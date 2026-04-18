using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using static Avalonia.OpenGL.GlConsts;

namespace CarolusNexus;

/// <summary>
/// Kleine 3D-Szene (Papier + Metallakzent) per OpenGL — nur innerhalb der OpenGL-Callbacks mit <see cref="GlInterface"/> arbeiten.
/// </summary>
public sealed class OfficeScene3D : OpenGlControlBase
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private int _vertexShader;
    private int _fragmentShader;
    private int _program;
    private int _vbo;
    private int _ibo;
    private int _vao;
    private int _indexCount;

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
    }

    private static string AdaptShaderSource(bool fragment, string shader, GlVersion version)
    {
        var glslVersion = version.Type == GlProfileType.OpenGL
            ? (OperatingSystem.IsMacOS() ? 150 : 120)
            : 100;

        var header = new StringBuilder();
        header.Append("#version ").Append(glslVersion).Append('\n');
        if (version.Type == GlProfileType.OpenGLES)
            header.Append("precision mediump float;\n");

        if (glslVersion >= 150)
        {
            shader = shader.Replace("attribute", "in", StringComparison.Ordinal);
            if (fragment)
                shader = shader
                    .Replace("varying", "in", StringComparison.Ordinal)
                    .Replace("//DECLARE_OUT", "out vec4 fragColor;", StringComparison.Ordinal)
                    .Replace("gl_FragColor", "fragColor", StringComparison.Ordinal);
            else
                shader = shader.Replace("varying", "out", StringComparison.Ordinal);
        }
        else
        {
            if (fragment)
                shader = shader.Replace("//DECLARE_OUT", "", StringComparison.Ordinal);
        }

        header.Append(shader);
        return header.ToString();
    }

    private static (Vertex[] Vertices, ushort[] Indices) BuildCube()
    {
        var verts = new List<Vertex>();
        var indices = new List<ushort>();
        const float h = 0.5f;

        void Face(Vector3 n, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var b0 = (ushort)verts.Count;
            verts.Add(new Vertex { Position = a, Normal = n });
            verts.Add(new Vertex { Position = b, Normal = n });
            verts.Add(new Vertex { Position = c, Normal = n });
            verts.Add(new Vertex { Position = d, Normal = n });
            indices.Add(b0);
            indices.Add((ushort)(b0 + 1));
            indices.Add((ushort)(b0 + 2));
            indices.Add(b0);
            indices.Add((ushort)(b0 + 2));
            indices.Add((ushort)(b0 + 3));
        }

        Face(new Vector3(0, 0, 1),
            new Vector3(-h, -h, h), new Vector3(h, -h, h), new Vector3(h, h, h), new Vector3(-h, h, h));
        Face(new Vector3(0, 0, -1),
            new Vector3(h, -h, -h), new Vector3(-h, -h, -h), new Vector3(-h, h, -h), new Vector3(h, h, -h));
        Face(new Vector3(1, 0, 0),
            new Vector3(h, -h, -h), new Vector3(h, -h, h), new Vector3(h, h, h), new Vector3(h, h, -h));
        Face(new Vector3(-1, 0, 0),
            new Vector3(-h, -h, h), new Vector3(-h, -h, -h), new Vector3(-h, h, -h), new Vector3(-h, h, h));
        Face(new Vector3(0, 1, 0),
            new Vector3(-h, h, h), new Vector3(h, h, h), new Vector3(h, h, -h), new Vector3(-h, h, -h));
        Face(new Vector3(0, -1, 0),
            new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, -h, h), new Vector3(-h, -h, h));

        return (verts.ToArray(), indices.ToArray());
    }

    private readonly Vertex[] _meshVertices;
    private readonly ushort[] _meshIndices;

    public OfficeScene3D()
    {
        (_meshVertices, _meshIndices) = BuildCube();
        _indexCount = _meshIndices.Length;
    }

    protected override unsafe void OnOpenGlInit(GlInterface gl)
    {
        var vsSrc = AdaptShaderSource(false, @"
attribute vec3 aPos;
attribute vec3 aNormal;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
uniform vec3 uColor;
varying vec3 vNormal;
varying vec3 vPos;
void main() {
  vNormal = mat3(uModel) * aNormal;
  vec4 wp = uModel * vec4(aPos, 1.0);
  vPos = wp.xyz;
  gl_Position = uProjection * uView * wp;
}
", gl.ContextInfo.Version);

        var fsSrc = AdaptShaderSource(true, @"
varying vec3 vNormal;
varying vec3 vPos;
uniform float uR;
uniform float uG;
uniform float uB;
uniform float uLx;
uniform float uLy;
uniform float uLz;
uniform float uTime;
//DECLARE_OUT
void main() {
  vec3 uColor = vec3(uR, uG, uB);
  vec3 uLightPos = vec3(uLx, uLy, uLz);
  vec3 N = normalize(vNormal);
  vec3 L = normalize(uLightPos - vPos);
  vec3 V = normalize(-vPos);
  vec3 H = normalize(L + V);
  float diff = max(dot(N, L), 0.0);
  float spec = pow(max(dot(N, H), 0.0), 48.0);
  float rim = pow(1.0 - max(dot(N, V), 0.0), 2.5);
  vec3 base = uColor * (0.22 + 0.78 * diff);
  vec3 hi = vec3(1.0, 0.98, 0.92) * spec * 0.35;
  vec3 edge = uColor * 1.15 * rim * 0.35;
  float pulse = 0.04 * sin(uTime * 2.2);
  gl_FragColor = vec4(base + hi + edge + pulse, 1.0);
}
", gl.ContextInfo.Version);

        _vertexShader = gl.CreateShader(GL_VERTEX_SHADER);
        var vsErr = gl.CompileShaderAndGetError(_vertexShader, vsSrc);
        if (vsErr != null)
            throw new InvalidOperationException("Vertex shader: " + vsErr);

        _fragmentShader = gl.CreateShader(GL_FRAGMENT_SHADER);
        var fsErr = gl.CompileShaderAndGetError(_fragmentShader, fsSrc);
        if (fsErr != null)
            throw new InvalidOperationException("Fragment shader: " + fsErr);

        _program = gl.CreateProgram();
        gl.AttachShader(_program, _vertexShader);
        gl.AttachShader(_program, _fragmentShader);
        const int posLoc = 0;
        const int nLoc = 1;
        gl.BindAttribLocationString(_program, posLoc, "aPos");
        gl.BindAttribLocationString(_program, nLoc, "aNormal");
        var linkErr = gl.LinkProgramAndGetError(_program);
        if (linkErr != null)
            throw new InvalidOperationException("Program link: " + linkErr);

        _vbo = gl.GenBuffer();
        gl.BindBuffer(GL_ARRAY_BUFFER, _vbo);
        var stride = Marshal.SizeOf<Vertex>();
        fixed (void* p = _meshVertices)
            gl.BufferData(GL_ARRAY_BUFFER, new IntPtr(_meshVertices.Length * stride), new IntPtr(p), GL_STATIC_DRAW);

        _ibo = gl.GenBuffer();
        gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, _ibo);
        fixed (void* ip = _meshIndices)
            gl.BufferData(GL_ELEMENT_ARRAY_BUFFER, new IntPtr(_meshIndices.Length * sizeof(ushort)), new IntPtr(ip),
                GL_STATIC_DRAW);

        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);
        gl.BindBuffer(GL_ARRAY_BUFFER, _vbo);
        gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, _ibo);
        gl.VertexAttribPointer(posLoc, 3, GL_FLOAT, 0, stride, IntPtr.Zero);
        gl.VertexAttribPointer(nLoc, 3, GL_FLOAT, 0, stride, new IntPtr(12));
        gl.EnableVertexAttribArray(posLoc);
        gl.EnableVertexAttribArray(nLoc);
        gl.BindVertexArray(0);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        gl.BindBuffer(GL_ARRAY_BUFFER, 0);
        gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, 0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);

        gl.DeleteBuffer(_vbo);
        gl.DeleteBuffer(_ibo);
        gl.DeleteVertexArray(_vao);
        gl.DeleteProgram(_program);
        gl.DeleteShader(_vertexShader);
        gl.DeleteShader(_fragmentShader);
    }

    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        var size = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
        if (size.Width <= 0 || size.Height <= 0)
            return;

        gl.BindFramebuffer(GL_FRAMEBUFFER, fb);
        gl.Viewport(0, 0, size.Width, size.Height);
        gl.Enable(GL_DEPTH_TEST);
        gl.ClearColor(0.06f, 0.07f, 0.09f, 1f);
        gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

        var aspect = (float)size.Width / size.Height;
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(float.Pi / 4f, aspect, 0.05f, 100f);
        var t = (float)_clock.Elapsed.TotalSeconds;
        var cam = Matrix4x4.CreateTranslation(0f, 0.15f, 0f) *
                  Matrix4x4.CreateRotationY(t * 0.55f) *
                  Matrix4x4.CreateRotationX(0.35f + 0.08f * MathF.Sin(t * 0.7f));
        var view = Matrix4x4.CreateLookAt(new Vector3(0f, 1.1f, 3.2f), new Vector3(0f, 0f, 0f), Vector3.UnitY);

        var lightWorld = new Vector3(2.2f, 3.5f, 2.0f);

        gl.UseProgram(_program);
        gl.BindVertexArray(_vao);

        var locModel = gl.GetUniformLocationString(_program, "uModel");
        var locView = gl.GetUniformLocationString(_program, "uView");
        var locProj = gl.GetUniformLocationString(_program, "uProjection");
        var locR = gl.GetUniformLocationString(_program, "uR");
        var locG = gl.GetUniformLocationString(_program, "uG");
        var locB = gl.GetUniformLocationString(_program, "uB");
        var locLx = gl.GetUniformLocationString(_program, "uLx");
        var locLy = gl.GetUniformLocationString(_program, "uLy");
        var locLz = gl.GetUniformLocationString(_program, "uLz");
        var locTime = gl.GetUniformLocationString(_program, "uTime");

        gl.UniformMatrix4fv(locView, 1, false, &view);
        gl.UniformMatrix4fv(locProj, 1, false, &projection);
        gl.Uniform1f(locLx, lightWorld.X);
        gl.Uniform1f(locLy, lightWorld.Y);
        gl.Uniform1f(locLz, lightWorld.Z);
        gl.Uniform1f(locTime, t);

        void SetRgb(Vector3 c)
        {
            gl.Uniform1f(locR, c.X);
            gl.Uniform1f(locG, c.Y);
            gl.Uniform1f(locB, c.Z);
        }

        // „Papier“-Platte (flach, cremig)
        var paper = Matrix4x4.CreateScale(1.35f, 0.12f, 1.05f) * cam;
        var paperColor = new Vector3(1f, 0.96f, 0.82f);
        gl.UniformMatrix4fv(locModel, 1, false, &paper);
        SetRgb(paperColor);
        gl.DrawElements(GL_TRIANGLES, _indexCount, GL_UNSIGNED_SHORT, IntPtr.Zero);

        // „Büroklammer“-Kern (schmaler Metallblock, leicht versetzt)
        var clip = Matrix4x4.CreateScale(0.14f, 0.55f, 0.38f) *
                   Matrix4x4.CreateTranslation(0.35f, 0.42f, 0.12f) *
                   Matrix4x4.CreateRotationZ(0.35f + 0.2f * MathF.Sin(t * 1.3f)) *
                   cam;
        var steel = new Vector3(0.62f, 0.66f, 0.72f);
        gl.UniformMatrix4fv(locModel, 1, false, &clip);
        SetRgb(steel);
        gl.DrawElements(GL_TRIANGLES, _indexCount, GL_UNSIGNED_SHORT, IntPtr.Zero);

        // kleiner Akzentwürfel (orbitartig)
        var orbit = Matrix4x4.CreateScale(0.2f) *
                    Matrix4x4.CreateTranslation(1.05f, 0f, 0f) *
                    Matrix4x4.CreateRotationY(t * 1.8f) *
                    Matrix4x4.CreateTranslation(0f, 0.35f, 0f) *
                    cam;
        var accent = new Vector3(0.95f, 0.55f, 0.2f);
        gl.UniformMatrix4fv(locModel, 1, false, &orbit);
        SetRgb(accent);
        gl.DrawElements(GL_TRIANGLES, _indexCount, GL_UNSIGNED_SHORT, IntPtr.Zero);

        gl.BindVertexArray(0);
        gl.Flush();
        RequestNextFrameRendering();
    }
}
