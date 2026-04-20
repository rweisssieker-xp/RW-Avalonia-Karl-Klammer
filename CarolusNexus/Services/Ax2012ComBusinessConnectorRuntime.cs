using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>
/// Reflexionszugriff auf die installierte <c>Microsoft.Dynamics.BusinessConnectorNet.dll</c> (AX 2012),
/// ohne Assembly-Referenz im Projekt — nur auf Maschinen mit AX-Client sinnvoll.
/// </summary>
public static class Ax2012ComBusinessConnectorRuntime
{
    /// <summary>Logon mit vier String-Parametern (übliches AX-2012-Muster), danach Logoff wenn vorhanden.</summary>
    public static string TryLogonProbe(NexusSettings settings)
    {
        if (!OperatingSystem.IsWindows())
            return "[SKIP] COM BC only on Windows";

        var dll = (settings.AxBusinessConnectorNetAssemblyPath ?? "").Trim();
        if (string.IsNullOrEmpty(dll) || !File.Exists(dll))
            return "[SKIP] AxBusinessConnectorNetAssemblyPath missing or file not found (install AX client / set path).";

        var company = (settings.AxDataAreaId ?? "").Trim();
        var lang = (settings.AxBcLanguage ?? "en-us").Trim();
        var aos = (settings.AxBcObjectServer ?? "").Trim();
        var db = (settings.AxBcDatabase ?? "").Trim();

        if (company.Length == 0 || aos.Length == 0 || db.Length == 0)
            return "[SKIP] AxDataAreaId, AxBcObjectServer and AxBcDatabase required for COM logon probe.";

        object? ax = null;
        try
        {
            var asm = Assembly.LoadFrom(dll);
            var axType =
                asm.GetType("Microsoft.Dynamics.BusinessConnectorNet.Axapta")
                ?? asm.GetTypes().FirstOrDefault(t => t.Name == "Axapta");

            if (axType == null)
                return "[ERR] Type Axapta not found in " + dll;

            ax = Activator.CreateInstance(axType);
            if (ax == null)
                return "[ERR] Axapta ctor returned null";

            var logon = axType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name is "Logon" or "LogonAs" && m.GetParameters().Length == 4);
            if (logon == null)
                return "[ERR] No Logon(string,string,string,string) on Axapta — check BC version.";

            logon.Invoke(ax, new object[] { company, lang, aos, db });

            var logoff = axType.GetMethod("Logoff", BindingFlags.Instance | BindingFlags.Public, Type.DefaultBinder, Type.EmptyTypes, null);
            logoff?.Invoke(ax, null);

            return "[OK] COM Business Connector: Logon + Logoff probe succeeded (test tenant: " + company + ").";
        }
        catch (Exception ex)
        {
            return "[ERR] COM BC: " + ex.Message;
        }
    }
}
