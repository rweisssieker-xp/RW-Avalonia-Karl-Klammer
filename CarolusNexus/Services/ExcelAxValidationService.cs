using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public sealed record ExcelAxColumnProfile(string Name, int Index, int NonEmptyCount, bool LooksLikeKey);

public sealed record ExcelAxSourcePreview(
    string FilePath,
    string SheetName,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    IReadOnlyList<ExcelAxColumnProfile> Profiles,
    string SuggestedKeyColumn);

public sealed record ExcelAxValidationOptions(
    string Preset,
    string KeyColumn,
    string AxEntity,
    string AxKeyField,
    int MaxRows);

public sealed record ExcelAxReconciliationInsight(
    string Category,
    string Severity,
    string Explanation,
    string NextAction,
    string EvidenceHint);

public sealed record ExcelAxValidationRow(
    int RowNumber,
    string Key,
    string Status,
    string AxEvidence,
    string Note,
    ExcelAxReconciliationInsight Insight);

public sealed record ExcelAxValidationRun(
    string RunId,
    DateTimeOffset CreatedUtc,
    string FilePath,
    ExcelAxValidationOptions Options,
    IReadOnlyList<ExcelAxValidationRow> Rows,
    string Summary,
    string ExportPath);

public sealed record ExcelAxReadinessScore(
    int Score,
    string Grade,
    string Verdict,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Strengths);

public sealed record ExcelAxExceptionItem(
    string Category,
    string Severity,
    int Count,
    string Owner,
    string FirstAction,
    string SampleKeys);

public sealed record ExcelAxPilotPack(
    string MarkdownPath,
    string CsvPath,
    string JsonPath,
    string Summary);

public sealed record ExcelAxRunDiff(
    string PreviousRunId,
    int CurrentScore,
    int PreviousScore,
    int ScoreDelta,
    IReadOnlyList<string> NewExceptions,
    IReadOnlyList<string> ResolvedExceptions,
    IReadOnlyList<string> PersistentExceptions);

public sealed record ExcelAxSafeModeCertificate(
    string CertificatePath,
    string Statement,
    IReadOnlyList<string> Guarantees);

public sealed record ExcelAxFieldMapping(string ExcelColumn, string AxField, int Confidence, string Reason);

public sealed record ExcelAxRiskCell(string Scope, string Target, string Severity, string Reason);

public sealed record ExcelAxDuplicateCluster(string ClusterKey, IReadOnlyList<string> Members, string Reason);

public sealed record ExcelAxOperatorTask(string Owner, string Priority, string Title, string Action, string Evidence);

public sealed record ExcelAxRoiEstimate(int AutomatedChecks, int ManualCases, double EstimatedMinutesSaved, string Summary);

public sealed record ExcelAxFixExport(string CsvPath, int Rows, string Summary);

public sealed record ExcelAxEvidenceBundle(string ZipPath, IReadOnlyList<string> IncludedFiles, string Summary);

public static class ExcelAxValidationService
{
    public static readonly string[] Presets =
    [
        "Debitor/Kunde",
        "Kreditor/Lieferant",
        "Artikel",
        "Sachkonto/Kostenstelle",
        "Custom OData"
    ];

    public static ExcelAxSourcePreview LoadPreview(string filePath, int maxRows = 80)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var rows = ext switch
        {
            ".csv" => ReadCsv(filePath, maxRows + 1),
            ".xlsx" => ReadXlsx(filePath, maxRows + 1, out _),
            _ => throw new InvalidOperationException("Only .xlsx and .csv are supported.")
        };

        if (rows.Count == 0)
            throw new InvalidOperationException("File contains no readable rows.");

        var columns = rows[0].Select((c, i) => string.IsNullOrWhiteSpace(c) ? $"Column {i + 1}" : c.Trim()).ToArray();
        var data = rows.Skip(1).Take(maxRows).Select(r => NormalizeWidth(r, columns.Length)).ToArray();
        var profiles = BuildProfiles(columns, data);
        var suggested = profiles.OrderByDescending(p => p.LooksLikeKey).ThenByDescending(p => p.NonEmptyCount).FirstOrDefault()?.Name
                        ?? columns.FirstOrDefault()
                        ?? "";
        var sheet = ext == ".xlsx" ? GetFirstSheetName(filePath) : "CSV";
        return new ExcelAxSourcePreview(filePath, sheet, columns, data, profiles, suggested);
    }

    public static ExcelAxValidationOptions BuildDefaultOptions(ExcelAxSourcePreview preview, string? preset = null)
    {
        var p = string.IsNullOrWhiteSpace(preset) ? Presets[0] : preset.Trim();
        var (entity, keyField) = p switch
        {
            "Kreditor/Lieferant" => ("Vendors", "AccountNum"),
            "Artikel" => ("ReleasedProducts", "ItemId"),
            "Sachkonto/Kostenstelle" => ("MainAccounts", "MainAccountId"),
            "Custom OData" => ("", ""),
            _ => ("Customers", "AccountNum")
        };

        return new ExcelAxValidationOptions(p, preview.SuggestedKeyColumn, entity, keyField, 500);
    }

    public static ExcelAxValidationRun Validate(string filePath, ExcelAxValidationOptions options, NexusSettings settings)
    {
        var preview = LoadPreview(filePath, Math.Max(options.MaxRows, 1));
        var keyIndex = preview.Columns
            .Select((name, index) => new { name, index })
            .FirstOrDefault(x => string.Equals(x.name, options.KeyColumn, StringComparison.OrdinalIgnoreCase))
            ?.index ?? -1;
        if (keyIndex < 0)
            throw new InvalidOperationException("Key column not found: " + options.KeyColumn);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<ExcelAxValidationRow>();
        var rowNumber = 1;
        foreach (var row in preview.Rows.Take(Math.Max(options.MaxRows, 1)))
        {
            rowNumber++;
            var key = keyIndex < row.Count ? (row[keyIndex] ?? "").Trim() : "";
            if (string.IsNullOrWhiteSpace(key))
            {
                rows.Add(CreateRow(rowNumber, "", "MissingKey", "", "Excel key is empty.", options));
                continue;
            }

            if (!seen.Add(key))
            {
                rows.Add(CreateRow(rowNumber, key, "DuplicateInExcel", "", "Key appears more than once in the Excel list.", options));
                continue;
            }

            var (status, evidence, note) = LookupAx(key, options, settings);
            rows.Add(CreateRow(rowNumber, key, status, evidence, note, options));
        }

        var runId = "excel-ax-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        Directory.CreateDirectory(AppPaths.ExcelAxChecksDir);
        var exportPath = Path.Combine(AppPaths.ExcelAxChecksDir, runId + ".csv");
        var run = new ExcelAxValidationRun(
            runId,
            DateTimeOffset.UtcNow,
            filePath,
            options,
            rows,
            BuildSummary(rows, settings),
            exportPath);
        ExportCsv(run, exportPath);
        File.WriteAllText(Path.Combine(AppPaths.ExcelAxChecksDir, runId + ".json"), JsonSerializer.Serialize(run, new JsonSerializerOptions { WriteIndented = true }));
        return run;
    }

    public static IReadOnlyList<ExcelAxFieldMapping> BuildFieldMappings(ExcelAxSourcePreview preview, ExcelAxValidationOptions options)
    {
        return preview.Columns
            .Select(column =>
            {
                var normalized = NormalizeToken(column);
                var mapping = GuessAxField(normalized, column, options);
                return new ExcelAxFieldMapping(column, mapping.Field, mapping.Confidence, mapping.Reason);
            })
            .OrderByDescending(m => m.Confidence)
            .ThenBy(m => m.ExcelColumn)
            .ToArray();
    }

    public static IReadOnlyList<ExcelAxRiskCell> BuildRiskHeatmap(ExcelAxSourcePreview preview, ExcelAxValidationRun? run = null)
    {
        var risks = new List<ExcelAxRiskCell>();
        foreach (var profile in preview.Profiles)
        {
            var fillRate = preview.Rows.Count == 0 ? 0 : profile.NonEmptyCount * 100 / Math.Max(preview.Rows.Count, 1);
            if (profile.LooksLikeKey && fillRate < 100)
                risks.Add(new ExcelAxRiskCell("Column", profile.Name, "blocked", $"Key-like column has only {fillRate}% filled cells."));
            else if (fillRate < 60)
                risks.Add(new ExcelAxRiskCell("Column", profile.Name, "warning", $"Column is sparse ({fillRate}% filled)."));
            if (NormalizeToken(profile.Name).Contains("dataarea", StringComparison.Ordinal) || NormalizeToken(profile.Name).Contains("company", StringComparison.Ordinal) || NormalizeToken(profile.Name).Contains("mandant", StringComparison.Ordinal))
                risks.Add(new ExcelAxRiskCell("Column", profile.Name, "review", "Column may control AX company/DataArea context."));
        }

        if (run != null)
        {
            risks.AddRange(run.Rows
                .Where(r => !string.Equals(r.Insight.Severity, "ok", StringComparison.OrdinalIgnoreCase))
                .Take(60)
                .Select(r => new ExcelAxRiskCell("Row", r.RowNumber.ToString(CultureInfo.InvariantCulture), r.Insight.Severity, $"{r.Status}: {r.Insight.NextAction}")));
        }

        return risks
            .OrderByDescending(r => SeverityWeight(r.Severity))
            .ThenBy(r => r.Scope)
            .ThenBy(r => r.Target)
            .ToArray();
    }

    public static string BuildRiskHeatmapReport(ExcelAxSourcePreview preview, ExcelAxValidationRun? run = null)
    {
        var risks = BuildRiskHeatmap(preview, run);
        var sb = new StringBuilder();
        sb.AppendLine("Excel Risk Heatmap");
        if (risks.Count == 0)
        {
            sb.AppendLine("- No obvious structural Excel risks detected.");
            return sb.ToString().TrimEnd();
        }
        foreach (var risk in risks.Take(80))
            sb.AppendLine($"- [{risk.Severity}] {risk.Scope} {risk.Target}: {risk.Reason}");
        return sb.ToString().TrimEnd();
    }

    public static string BuildFieldMappingReport(ExcelAxSourcePreview preview, ExcelAxValidationOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AX Field Mapping Intelligence");
        foreach (var mapping in BuildFieldMappings(preview, options))
            sb.AppendLine($"- {mapping.ExcelColumn} -> {mapping.AxField} ({mapping.Confidence}%) | {mapping.Reason}");
        return sb.ToString().TrimEnd();
    }

    public static string BuildDataAreaCopilotReport(ExcelAxSourcePreview preview)
    {
        var sb = new StringBuilder();
        sb.AppendLine("DataArea / Company Copilot");
        var dataAreaIndex = preview.Columns
            .Select((name, index) => new { name, index, token = NormalizeToken(name) })
            .FirstOrDefault(x => x.token.Contains("dataarea", StringComparison.Ordinal) || x.token.Contains("company", StringComparison.Ordinal) || x.token.Contains("mandant", StringComparison.Ordinal))
            ?.index ?? -1;
        if (dataAreaIndex >= 0)
        {
            var values = preview.Rows
                .Select(r => dataAreaIndex < r.Count ? r[dataAreaIndex].Trim() : "")
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Take(8)
                .Select(g => $"{g.Key}: {g.Count()}")
                .ToArray();
            sb.AppendLine("- Explicit DataArea/company column found: " + preview.Columns[dataAreaIndex]);
            sb.AppendLine("- Values: " + (values.Length == 0 ? "none" : string.Join("; ", values)));
            if (values.Length > 1)
                sb.AppendLine("- Review: multiple company values detected; verify AX company context before comparing.");
        }
        else
        {
            sb.AppendLine("- No explicit DataArea/company column detected.");
            sb.AppendLine("- Recommendation: add DataAreaId/company context for multi-company AX 2012 environments.");
        }
        return sb.ToString().TrimEnd();
    }

    public static IReadOnlyList<ExcelAxDuplicateCluster> BuildDuplicateIntelligence(ExcelAxSourcePreview preview)
    {
        var nameIndex = preview.Columns
            .Select((name, index) => new { name, index, token = NormalizeToken(name) })
            .FirstOrDefault(x => x.token.Contains("name", StringComparison.Ordinal) || x.token.Contains("firma", StringComparison.Ordinal) || x.token.Contains("bezeichnung", StringComparison.Ordinal))
            ?.index ?? -1;
        if (nameIndex < 0)
            return [];
        return preview.Rows
            .Select(r => nameIndex < r.Count ? r[nameIndex].Trim() : "")
            .Where(v => v.Length >= 4)
            .GroupBy(v => DuplicateToken(v), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1 || g.Count() > 1)
            .Select(g => new ExcelAxDuplicateCluster(g.Key, g.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray(), "Similar normalized business names."))
            .Take(20)
            .ToArray();
    }

    public static string BuildDuplicateIntelligenceReport(ExcelAxSourcePreview preview)
    {
        var clusters = BuildDuplicateIntelligence(preview);
        var sb = new StringBuilder();
        sb.AppendLine("Duplicate Intelligence 2.0");
        if (clusters.Count == 0)
        {
            sb.AppendLine("- No fuzzy duplicate clusters detected in name-like columns.");
            return sb.ToString().TrimEnd();
        }
        foreach (var cluster in clusters)
            sb.AppendLine($"- {cluster.ClusterKey}: {string.Join(" | ", cluster.Members)} | {cluster.Reason}");
        return sb.ToString().TrimEnd();
    }

    public static string BuildFixProposalPackReport(ExcelAxValidationRun run)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AI Fix Proposal Pack");
        foreach (var item in BuildOperatorTaskBoard(run).Take(40))
            sb.AppendLine($"- [{item.Priority}] {item.Owner}: {item.Title} -> {item.Action} ({item.Evidence})");
        return sb.ToString().TrimEnd();
    }

    public static ExcelAxRoiEstimate BuildRoiEstimate(ExcelAxValidationRun run)
    {
        var automated = run.Rows.Count;
        var manual = run.Rows.Count(r => !string.Equals(r.Insight.Severity, "ok", StringComparison.OrdinalIgnoreCase));
        var saved = Math.Round((automated * 1.5) + Math.Max(0, automated - manual) * 0.75, 1);
        var summary = $"{automated} checks prepared; {manual} manual cases remain; approx. {saved.ToString("0.0", CultureInfo.InvariantCulture)} minutes saved in first-pass review.";
        return new ExcelAxRoiEstimate(automated, manual, saved, summary);
    }

    public static string BuildRoiEstimatorReport(ExcelAxValidationRun run)
    {
        var roi = BuildRoiEstimate(run);
        return "Pilot ROI Estimator\n"
            + $"- Automated checks: {roi.AutomatedChecks}\n"
            + $"- Manual cases remaining: {roi.ManualCases}\n"
            + $"- Estimated minutes saved: {roi.EstimatedMinutesSaved.ToString("0.0", CultureInfo.InvariantCulture)}\n"
            + "- Summary: " + roi.Summary;
    }

    public static string BuildProcessTwinReport(ExcelAxValidationRun run)
    {
        var score = BuildReadinessScore(run);
        var sb = new StringBuilder();
        sb.AppendLine("AX Process Twin");
        sb.AppendLine($"- Intake: completed ({run.Rows.Count} rows)");
        sb.AppendLine($"- Mapping: {run.Options.KeyColumn} -> {run.Options.AxEntity}.{run.Options.AxKeyField}");
        sb.AppendLine($"- Reconciliation: {score.Score}/100 ({score.Grade})");
        sb.AppendLine("- Exception triage: " + (BuildExceptionInbox(run).Count == 0 ? "clear" : "open"));
        sb.AppendLine("- AX mutation: locked by safe-mode workflow");
        sb.AppendLine("- Next gate: " + score.Verdict);
        return sb.ToString().TrimEnd();
    }

    public static string BuildEvidenceTimelineReport(ExcelAxValidationRun run)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Evidence Timeline");
        sb.AppendLine($"- {run.CreatedUtc:O}: validation run created ({run.RunId})");
        sb.AppendLine($"- {run.CreatedUtc:O}: CSV evidence written ({run.ExportPath})");
        sb.AppendLine($"- {run.CreatedUtc:O}: JSON evidence written ({Path.ChangeExtension(run.ExportPath, ".json")})");
        var pack = Path.Combine(AppPaths.ExcelAxChecksDir, run.RunId + "-pilot-pack.md");
        var cert = Path.Combine(AppPaths.ExcelAxChecksDir, run.RunId + "-safe-mode-certificate.md");
        sb.AppendLine(File.Exists(pack) ? $"- {File.GetLastWriteTimeUtc(pack):O}: pilot pack written ({pack})" : "- Pilot pack: not generated yet");
        sb.AppendLine(File.Exists(cert) ? $"- {File.GetLastWriteTimeUtc(cert):O}: safe-mode certificate written ({cert})" : "- Safe-mode certificate: not generated yet");
        return sb.ToString().TrimEnd();
    }

    public static IReadOnlyList<ExcelAxOperatorTask> BuildOperatorTaskBoard(ExcelAxValidationRun run)
    {
        return BuildExceptionInbox(run)
            .Select(item => new ExcelAxOperatorTask(
                item.Owner,
                PriorityForSeverity(item.Severity),
                $"{item.Category}: {item.Count} item(s)",
                item.FirstAction,
                item.SampleKeys))
            .ToArray();
    }

    public static string BuildOperatorTaskBoardReport(ExcelAxValidationRun run)
    {
        var tasks = BuildOperatorTaskBoard(run);
        var sb = new StringBuilder();
        sb.AppendLine("Operator Task Board");
        if (tasks.Count == 0)
        {
            sb.AppendLine("- No open operator tasks.");
            return sb.ToString().TrimEnd();
        }
        foreach (var task in tasks)
            sb.AppendLine($"- [{task.Priority}] {task.Owner}: {task.Title} -> {task.Action} | {task.Evidence}");
        return sb.ToString().TrimEnd();
    }

    public static string AnswerOfflineAxKnowledge(string question)
    {
        var q = (question ?? "").ToLowerInvariant();
        if (q.Contains("debitor", StringComparison.Ordinal) || q.Contains("customer", StringComparison.Ordinal) || q.Contains("kunde", StringComparison.Ordinal))
            return "Offline AX Knowledge\n- Debitor/customer checks usually start with CustTable.AccountNum, company/DataArea context, blocked status and address/name plausibility.";
        if (q.Contains("kreditor", StringComparison.Ordinal) || q.Contains("vendor", StringComparison.Ordinal) || q.Contains("lieferant", StringComparison.Ordinal))
            return "Offline AX Knowledge\n- Vendor checks usually start with VendTable.AccountNum, company/DataArea context, payment setup and duplicate supplier names.";
        if (q.Contains("artikel", StringComparison.Ordinal) || q.Contains("item", StringComparison.Ordinal) || q.Contains("product", StringComparison.Ordinal))
            return "Offline AX Knowledge\n- Item checks usually start with ItemId/Product, released product context, unit, item group and company-specific release state.";
        if (q.Contains("odata", StringComparison.Ordinal))
            return "Offline AX Knowledge\n- AX 2012 OData checks are read-oriented here. Verify base URL, credentials, entity name, key field and company context before trusting empty results.";
        return "Offline AX Knowledge\n- Ask about Debitor/customer, Kreditor/vendor, Artikel/item or OData. The local answer stays read-only and does not require external AI.";
    }

    public static string BuildPreviewIntelligenceReport(ExcelAxSourcePreview preview, ExcelAxValidationOptions options, ExcelAxValidationRun? run = null)
    {
        return BuildRiskHeatmapReport(preview, run)
            + "\n\n" + BuildFieldMappingReport(preview, options)
            + "\n\n" + BuildDataAreaCopilotReport(preview)
            + "\n\n" + BuildDuplicateIntelligenceReport(preview);
    }

    public static ExcelAxFixExport CreateFixExportCsv(ExcelAxValidationRun run)
    {
        Directory.CreateDirectory(AppPaths.ExcelAxChecksDir);
        var path = Path.Combine(AppPaths.ExcelAxChecksDir, run.RunId + "-fix-export.csv");
        var sb = new StringBuilder();
        sb.AppendLine("runId,rowNumber,key,status,carolusOwner,carolusPriority,carolusFix,carolusConfidence,carolusEvidence");
        foreach (var row in run.Rows)
        {
            var priority = PriorityForSeverity(row.Insight.Severity);
            var confidence = row.Insight.Severity == "ok" ? 95 : row.Insight.Severity == "blocked" ? 90 : 75;
            sb.Append(Escape(run.RunId)).Append(',')
                .Append(row.RowNumber).Append(',')
                .Append(Escape(row.Key)).Append(',')
                .Append(Escape(row.Status)).Append(',')
                .Append(Escape(OwnerForCategory(row.Insight.Category))).Append(',')
                .Append(Escape(priority)).Append(',')
                .Append(Escape(row.Insight.NextAction)).Append(',')
                .Append(confidence).Append(',')
                .AppendLine(Escape(row.Insight.EvidenceHint));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return new ExcelAxFixExport(path, run.Rows.Count, $"Fix export created for {run.Rows.Count} row(s).");
    }

    public static ExcelAxEvidenceBundle CreateEvidenceBundleZip(ExcelAxValidationRun run)
    {
        Directory.CreateDirectory(AppPaths.ExcelAxChecksDir);
        var included = new List<string>();
        var pilot = CreatePilotPack(run);
        var cert = CreateSafeModeCertificate(run);
        var fix = CreateFixExportCsv(run);
        var zipPath = Path.Combine(AppPaths.ExcelAxChecksDir, run.RunId + "-evidence-bundle.zip");
        if (File.Exists(zipPath))
            File.Delete(zipPath);
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            AddZipEntry(archive, run.ExportPath, included);
            AddZipEntry(archive, Path.ChangeExtension(run.ExportPath, ".json"), included);
            AddZipEntry(archive, pilot.MarkdownPath, included);
            AddZipEntry(archive, cert.CertificatePath, included);
            AddZipEntry(archive, fix.CsvPath, included);
        }
        return new ExcelAxEvidenceBundle(zipPath, included, $"Evidence bundle created with {included.Count} artifact(s).");
    }

    public static string BuildRunDashboardReport()
    {
        var runs = LoadAllRuns().ToArray();
        var sb = new StringBuilder();
        sb.AppendLine("Run Dashboard");
        if (runs.Length == 0)
        {
            sb.AppendLine("- No Excel + AX runs found yet.");
            return sb.ToString().TrimEnd();
        }
        var scored = runs
            .Select(run => new { Run = run, Score = BuildReadinessScore(run), Open = BuildExceptionInbox(run).Sum(i => i.Count) })
            .OrderByDescending(x => x.Run.CreatedUtc)
            .ToArray();
        sb.AppendLine($"Runs: {scored.Length}");
        sb.AppendLine($"Average readiness: {Math.Round(scored.Average(x => x.Score.Score), 1).ToString("0.0", CultureInfo.InvariantCulture)}/100");
        sb.AppendLine($"Open exceptions: {scored.Sum(x => x.Open)}");
        sb.AppendLine();
        sb.AppendLine("Latest runs");
        foreach (var item in scored.Take(12))
            sb.AppendLine($"- {item.Run.CreatedUtc:yyyy-MM-dd HH:mm}; {item.Run.RunId}; score {item.Score.Score}/100 ({item.Score.Grade}); open {item.Open}; {Path.GetFileName(item.Run.FilePath)}");
        sb.AppendLine();
        sb.AppendLine("Worst readiness");
        foreach (var item in scored.OrderBy(x => x.Score.Score).Take(5))
            sb.AppendLine($"- {item.Score.Score}/100 ({item.Score.Grade}); {item.Run.RunId}; {item.Score.Verdict}");
        return sb.ToString().TrimEnd();
    }

    public static ExcelAxValidationRun? LoadPreviousRun(ExcelAxValidationRun current)
    {
        if (!Directory.Exists(AppPaths.ExcelAxChecksDir))
            return null;
        return Directory.EnumerateFiles(AppPaths.ExcelAxChecksDir, "excel-ax-*.json")
            .Where(path => !path.EndsWith(current.RunId + ".json", StringComparison.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Select(info =>
            {
                try
                {
                    return JsonSerializer.Deserialize<ExcelAxValidationRun>(File.ReadAllText(info.FullName));
                }
                catch
                {
                    return null;
                }
            })
            .FirstOrDefault(run => run != null && string.Equals(run.FilePath, current.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    public static string BuildRunReport(ExcelAxValidationRun run)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Excel + AX validation");
        sb.AppendLine("Run: " + run.RunId);
        sb.AppendLine("Input: " + run.FilePath);
        sb.AppendLine("Preset: " + run.Options.Preset);
        sb.AppendLine("Key: " + run.Options.KeyColumn + " -> " + run.Options.AxEntity + "." + run.Options.AxKeyField);
        sb.AppendLine("Export: " + run.ExportPath);
        sb.AppendLine();
        sb.AppendLine(run.Summary);
        sb.AppendLine();
        sb.AppendLine(BuildReconciliationCopilotReport(run));
        sb.AppendLine();
        foreach (var row in run.Rows.Take(80))
            sb.AppendLine($"{row.RowNumber}; {row.Key}; {row.Status}; {row.Insight.Category}/{row.Insight.Severity}; {row.Insight.NextAction}; {Short(row.AxEvidence, 180)}");
        if (run.Rows.Count > 80)
            sb.AppendLine("... truncated in UI; full export contains all rows.");
        return sb.ToString().TrimEnd();
    }

    public static ExcelAxRunDiff? BuildDiffAgainstPreviousRun(ExcelAxValidationRun current)
    {
        var previous = LoadPreviousRun(current);
        if (previous == null)
            return null;
        var currentScore = BuildReadinessScore(current).Score;
        var previousScore = BuildReadinessScore(previous).Score;
        var currentKeys = ExceptionKeys(current).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var previousKeys = ExceptionKeys(previous).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new ExcelAxRunDiff(
            previous.RunId,
            currentScore,
            previousScore,
            currentScore - previousScore,
            currentKeys.Except(previousKeys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).Take(20).ToArray(),
            previousKeys.Except(currentKeys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).Take(20).ToArray(),
            currentKeys.Intersect(previousKeys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).Take(20).ToArray());
    }

    public static string BuildRunDiffReport(ExcelAxValidationRun current)
    {
        var diff = BuildDiffAgainstPreviousRun(current);
        if (diff == null)
            return "Smart Diff\n- No previous run for this input file was found yet.";
        var sb = new StringBuilder();
        sb.AppendLine("Smart Diff against previous run");
        sb.AppendLine("Previous run: " + diff.PreviousRunId);
        sb.AppendLine($"Readiness: {diff.PreviousScore}/100 -> {diff.CurrentScore}/100 ({diff.ScoreDelta:+#;-#;0})");
        AppendDiffList(sb, "New exceptions", diff.NewExceptions);
        AppendDiffList(sb, "Resolved exceptions", diff.ResolvedExceptions);
        AppendDiffList(sb, "Persistent exceptions", diff.PersistentExceptions);
        return sb.ToString().TrimEnd();
    }

    public static string AnswerRunQuestion(ExcelAxValidationRun run, string question)
    {
        var q = (question ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(q))
            return "Ask a question about the latest run, for example: Which customers are missing in AX?";
        if (q.Contains("score", StringComparison.Ordinal) || q.Contains("ready", StringComparison.Ordinal) || q.Contains("readiness", StringComparison.Ordinal) || q.Contains("bereit", StringComparison.Ordinal))
            return BuildReadinessScoreReport(run);
        if (q.Contains("fehl", StringComparison.Ordinal) || q.Contains("missing", StringComparison.Ordinal) || q.Contains("not found", StringComparison.Ordinal))
            return BuildFilteredRowsAnswer(run, "Missing or not found", r => r.Status is "MissingKey" or "NotFound");
        if (q.Contains("duplicate", StringComparison.Ordinal) || q.Contains("duble", StringComparison.Ordinal) || q.Contains("dopp", StringComparison.Ordinal))
            return BuildFilteredRowsAnswer(run, "Duplicates", r => r.Status == "DuplicateInExcel");
        if (q.Contains("it", StringComparison.Ordinal) || q.Contains("odata", StringComparison.Ordinal) || q.Contains("ax offline", StringComparison.Ordinal) || q.Contains("connect", StringComparison.Ordinal))
            return BuildFilteredRowsAnswer(run, "AX/IT connectivity", r => r.Insight.Category == "AxConnectivity");
        if (q.Contains("block", StringComparison.Ordinal) || q.Contains("first", StringComparison.Ordinal) || q.Contains("zuerst", StringComparison.Ordinal) || q.Contains("priority", StringComparison.Ordinal))
            return BuildExceptionInboxReport(run);
        if (q.Contains("diff", StringComparison.Ordinal) || q.Contains("changed", StringComparison.Ordinal) || q.Contains("geändert", StringComparison.Ordinal) || q.Contains("gelöst", StringComparison.Ordinal))
            return BuildRunDiffReport(run);
        if (q.Contains("safe", StringComparison.Ordinal) || q.Contains("read-only", StringComparison.Ordinal) || q.Contains("certificate", StringComparison.Ordinal) || q.Contains("zertifikat", StringComparison.Ordinal))
            return BuildSafeModeCertificateReport(run);
        if (q.Contains("roi", StringComparison.Ordinal) || q.Contains("zeit", StringComparison.Ordinal) || q.Contains("sparen", StringComparison.Ordinal))
            return BuildRoiEstimatorReport(run);
        if (q.Contains("task", StringComparison.Ordinal) || q.Contains("owner", StringComparison.Ordinal) || q.Contains("aufgabe", StringComparison.Ordinal))
            return BuildOperatorTaskBoardReport(run);
        if (q.Contains("process", StringComparison.Ordinal) || q.Contains("prozess", StringComparison.Ordinal) || q.Contains("twin", StringComparison.Ordinal))
            return BuildProcessTwinReport(run);
        if (q.Contains("timeline", StringComparison.Ordinal) || q.Contains("audit", StringComparison.Ordinal) || q.Contains("evidence", StringComparison.Ordinal))
            return BuildEvidenceTimelineReport(run);
        if (q.Contains("ax knowledge", StringComparison.Ordinal) || q.Contains("debitor", StringComparison.Ordinal) || q.Contains("kreditor", StringComparison.Ordinal) || q.Contains("artikel", StringComparison.Ordinal))
            return AnswerOfflineAxKnowledge(question ?? "");
        return BuildReconciliationCopilotReport(run);
    }

    public static ExcelAxReadinessScore BuildReadinessScore(ExcelAxValidationRun run)
    {
        var total = Math.Max(run.Rows.Count, 1);
        var blocked = run.Rows.Count(r => string.Equals(InsightOf(r, run.Options).Severity, "blocked", StringComparison.OrdinalIgnoreCase));
        var warning = run.Rows.Count(r => string.Equals(InsightOf(r, run.Options).Severity, "warning", StringComparison.OrdinalIgnoreCase));
        var review = run.Rows.Count(r => string.Equals(InsightOf(r, run.Options).Severity, "review", StringComparison.OrdinalIgnoreCase));
        var ready = run.Rows.Count(r => string.Equals(InsightOf(r, run.Options).Severity, "ok", StringComparison.OrdinalIgnoreCase));
        var score = 100 - (blocked * 55 / total) - (warning * 18 / total) - (review * 28 / total);
        score = Math.Clamp(score, 0, 100);
        var grade = score >= 90 ? "A" : score >= 75 ? "B" : score >= 55 ? "C" : score >= 35 ? "D" : "E";
        var verdict = score switch
        {
            >= 90 => "Ready for controlled AX reconciliation.",
            >= 75 => "Mostly ready; clear remaining warnings before operational use.",
            >= 55 => "Pilot usable with manual review gates.",
            >= 35 => "Not ready; blocking data or AX access issues dominate.",
            _ => "Blocked; fix setup/data quality before pilot use."
        };
        var blockers = run.Rows
            .Select(r => InsightOf(r, run.Options))
            .Where(insight => SeverityWeight(insight.Severity) >= SeverityWeight("review"))
            .GroupBy(insight => insight.NextAction)
            .OrderByDescending(g => SeverityWeight(g.First().Severity))
            .ThenByDescending(g => g.Count())
            .Take(5)
            .Select(g => $"{g.Count()}x {g.Key}")
            .ToArray();
        var strengths = new List<string>
        {
            $"{ready}/{run.Rows.Count} rows are already classified as ready.",
            "Every row has an auditable local explanation and next action.",
            "CSV and JSON evidence are generated without AX write operations."
        };
        return new ExcelAxReadinessScore(score, grade, verdict, blockers, strengths);
    }

    public static IReadOnlyList<ExcelAxExceptionItem> BuildExceptionInbox(ExcelAxValidationRun run)
    {
        return run.Rows
            .Select(r => new { Row = r, Insight = InsightOf(r, run.Options) })
            .Where(x => !string.Equals(x.Insight.Severity, "ok", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => new { x.Insight.Category, x.Insight.Severity, x.Insight.NextAction })
            .OrderByDescending(g => SeverityWeight(g.Key.Severity))
            .ThenByDescending(g => g.Count())
            .Select(g => new ExcelAxExceptionItem(
                g.Key.Category,
                g.Key.Severity,
                g.Count(),
                OwnerForCategory(g.Key.Category),
                g.Key.NextAction,
                string.Join(", ", g.Select(x => string.IsNullOrWhiteSpace(x.Row.Key) ? $"row {x.Row.RowNumber}" : x.Row.Key).Distinct(StringComparer.OrdinalIgnoreCase).Take(6))))
            .ToArray();
    }

    public static string BuildExceptionInboxReport(ExcelAxValidationRun run)
    {
        var items = BuildExceptionInbox(run);
        var sb = new StringBuilder();
        sb.AppendLine("AI Exception Inbox");
        if (items.Count == 0)
        {
            sb.AppendLine("- No open exceptions. Keep evidence pack for audit.");
            return sb.ToString().TrimEnd();
        }

        foreach (var item in items)
            sb.AppendLine($"- [{item.Severity}] {item.Category}: {item.Count} items | Owner: {item.Owner} | Action: {item.FirstAction} | Keys: {item.SampleKeys}");
        return sb.ToString().TrimEnd();
    }

    public static string BuildReadinessScoreReport(ExcelAxValidationRun run)
    {
        var score = BuildReadinessScore(run);
        var sb = new StringBuilder();
        sb.AppendLine("AX Readiness Score");
        sb.AppendLine($"Score: {score.Score}/100 ({score.Grade})");
        sb.AppendLine("Verdict: " + score.Verdict);
        if (score.Blockers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Blockers");
            foreach (var blocker in score.Blockers)
                sb.AppendLine("- " + blocker);
        }
        sb.AppendLine();
        sb.AppendLine("Strengths");
        foreach (var strength in score.Strengths)
            sb.AppendLine("- " + strength);
        return sb.ToString().TrimEnd();
    }

    public static string BuildReconciliationCopilotReport(ExcelAxValidationRun run)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AI Reconciliation Copilot (local deterministic v1)");
        sb.AppendLine(BuildReadinessScoreReport(run));
        sb.AppendLine();
        sb.AppendLine(BuildExceptionInboxReport(run));
        sb.AppendLine();
        sb.AppendLine(BuildProcessTwinReport(run));
        sb.AppendLine();
        sb.AppendLine(BuildOperatorTaskBoardReport(run));
        sb.AppendLine();
        sb.AppendLine(BuildRoiEstimatorReport(run));
        sb.AppendLine();
        var groups = run.Rows
            .GroupBy(r => new { r.Insight.Category, r.Insight.Severity })
            .OrderByDescending(g => SeverityWeight(g.Key.Severity))
            .ThenByDescending(g => g.Count())
            .ThenBy(g => g.Key.Category);
        foreach (var group in groups)
            sb.AppendLine($"- {group.Key.Category}/{group.Key.Severity}: {group.Count()}");

        var actions = run.Rows
            .Where(r => !string.Equals(r.Insight.Severity, "ok", StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => r.Insight.NextAction)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Take(6)
            .ToArray();
        if (actions.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Top next actions");
            foreach (var action in actions)
                sb.AppendLine($"- {action.Count()}x {action.Key}");
        }

        var examples = run.Rows
            .Where(r => !string.Equals(r.Insight.Severity, "ok", StringComparison.OrdinalIgnoreCase))
            .Take(12)
            .ToArray();
        if (examples.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Examples");
            foreach (var row in examples)
                sb.AppendLine($"- Row {row.RowNumber}, key '{row.Key}': {row.Insight.Explanation} -> {row.Insight.NextAction}");
        }

        return sb.ToString().TrimEnd();
    }

    public static ExcelAxPilotPack CreatePilotPack(ExcelAxValidationRun run)
    {
        Directory.CreateDirectory(AppPaths.ExcelAxChecksDir);
        var markdownPath = Path.Combine(AppPaths.ExcelAxChecksDir, run.RunId + "-pilot-pack.md");
        var jsonPath = Path.ChangeExtension(run.ExportPath, ".json");
        var score = BuildReadinessScore(run);
        var sb = new StringBuilder();
        sb.AppendLine("# Carolus Nexus AX/Excel Pilot Pack");
        sb.AppendLine();
        sb.AppendLine("- Run: " + run.RunId);
        sb.AppendLine("- Created UTC: " + run.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
        sb.AppendLine("- Input: " + run.FilePath);
        sb.AppendLine("- Preset: " + run.Options.Preset);
        sb.AppendLine("- Key mapping: " + run.Options.KeyColumn + " -> " + run.Options.AxEntity + "." + run.Options.AxKeyField);
        sb.AppendLine("- CSV evidence: " + run.ExportPath);
        sb.AppendLine("- JSON evidence: " + jsonPath);
        sb.AppendLine();
        sb.AppendLine("## Management Summary");
        sb.AppendLine();
        sb.AppendLine($"AX readiness is {score.Score}/100 ({score.Grade}). {score.Verdict}");
        sb.AppendLine(run.Summary);
        sb.AppendLine();
        sb.AppendLine("## Readiness");
        sb.AppendLine();
        sb.AppendLine(BuildReadinessScoreReport(run));
        sb.AppendLine();
        sb.AppendLine("## Exception Inbox");
        sb.AppendLine();
        sb.AppendLine(BuildExceptionInboxReport(run));
        sb.AppendLine();
        sb.AppendLine("## Reconciliation Copilot");
        sb.AppendLine();
        sb.AppendLine(BuildReconciliationCopilotReport(run));
        sb.AppendLine();
        sb.AppendLine("## Fix Proposal Pack");
        sb.AppendLine();
        sb.AppendLine(BuildFixProposalPackReport(run));
        sb.AppendLine();
        sb.AppendLine("## Process Twin");
        sb.AppendLine();
        sb.AppendLine(BuildProcessTwinReport(run));
        sb.AppendLine();
        sb.AppendLine("## ROI Estimate");
        sb.AppendLine();
        sb.AppendLine(BuildRoiEstimatorReport(run));
        sb.AppendLine();
        sb.AppendLine("## Evidence Timeline");
        sb.AppendLine();
        sb.AppendLine(BuildEvidenceTimelineReport(run));
        sb.AppendLine();
        sb.AppendLine("## Audit Note");
        sb.AppendLine();
        sb.AppendLine("This pack is generated read-only. It does not write, post, book or mutate AX data.");
        File.WriteAllText(markdownPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return new ExcelAxPilotPack(markdownPath, run.ExportPath, jsonPath, $"Pilot pack created. Readiness {score.Score}/100 ({score.Grade}).");
    }

    public static ExcelAxSafeModeCertificate CreateSafeModeCertificate(ExcelAxValidationRun run)
    {
        Directory.CreateDirectory(AppPaths.ExcelAxChecksDir);
        var path = Path.Combine(AppPaths.ExcelAxChecksDir, run.RunId + "-safe-mode-certificate.md");
        var cert = BuildSafeModeCertificate(run, path);
        var sb = new StringBuilder();
        sb.AppendLine("# AX Safe-Mode Certificate");
        sb.AppendLine();
        sb.AppendLine("- Run: " + run.RunId);
        sb.AppendLine("- Created UTC: " + run.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
        sb.AppendLine("- Input: " + run.FilePath);
        sb.AppendLine("- Statement: " + cert.Statement);
        sb.AppendLine();
        sb.AppendLine("## Guarantees");
        foreach (var guarantee in cert.Guarantees)
            sb.AppendLine("- " + guarantee);
        sb.AppendLine();
        sb.AppendLine("## Evidence");
        sb.AppendLine("- CSV: " + run.ExportPath);
        sb.AppendLine("- JSON: " + Path.ChangeExtension(run.ExportPath, ".json"));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return cert;
    }

    public static string BuildSafeModeCertificateReport(ExcelAxValidationRun run)
    {
        var cert = BuildSafeModeCertificate(run, "");
        var sb = new StringBuilder();
        sb.AppendLine("AX Safe-Mode Certificate");
        sb.AppendLine(cert.Statement);
        foreach (var guarantee in cert.Guarantees)
            sb.AppendLine("- " + guarantee);
        return sb.ToString().TrimEnd();
    }

    private static ExcelAxValidationRow CreateRow(int rowNumber, string key, string status, string evidence, string note, ExcelAxValidationOptions options) =>
        new(rowNumber, key, status, evidence, note, BuildInsight(status, key, evidence, note, options));

    private static ExcelAxReconciliationInsight InsightOf(ExcelAxValidationRow row, ExcelAxValidationOptions options) =>
        row.Insight ?? BuildInsight(row.Status, row.Key, row.AxEvidence, row.Note, options);

    private static ExcelAxReconciliationInsight BuildInsight(string status, string key, string evidence, string note, ExcelAxValidationOptions options)
    {
        return status switch
        {
            "OK" => new ExcelAxReconciliationInsight(
                "Ready",
                "ok",
                "AX returned a matching record for the Excel key.",
                "No action required; keep the evidence export for audit.",
                "AX response contains the requested key."),
            "MissingKey" => new ExcelAxReconciliationInsight(
                "ExcelDataQuality",
                "blocked",
                "The Excel row has no usable key, so AX cannot be queried safely.",
                $"Fill '{options.KeyColumn}' before reconciliation.",
                "Empty Excel key cell."),
            "DuplicateInExcel" => new ExcelAxReconciliationInsight(
                "Duplicate",
                "warning",
                "The same key appears multiple times in the Excel list.",
                "Decide which Excel row is authoritative and remove or merge duplicates.",
                "Duplicate detected before AX lookup."),
            "AxUnavailable" => new ExcelAxReconciliationInsight(
                "AxConnectivity",
                "blocked",
                "AX could not be queried with the current setup.",
                "Enable AX integration, configure OData, or open AX foreground context for UIA evidence.",
                string.IsNullOrWhiteSpace(evidence) ? note : Short(evidence, 160)),
            "NotFound" => new ExcelAxReconciliationInsight(
                "AxMissingMasterData",
                "review",
                "No matching AX master data record was found for the Excel key.",
                "Verify company/DataArea, key format, or request/create the missing AX master data.",
                string.IsNullOrWhiteSpace(evidence) ? "AX response did not contain the key." : Short(evidence, 160)),
            "NeedsManualReview" => new ExcelAxReconciliationInsight(
                "ManualReview",
                "review",
                "The run produced contextual evidence but no deterministic AX match.",
                "Use captured AX foreground evidence and decide manually.",
                string.IsNullOrWhiteSpace(evidence) ? note : Short(evidence, 160)),
            _ => new ExcelAxReconciliationInsight(
                "Configuration",
                "review",
                "The validation status is not mapped to a known automation outcome.",
                "Review preset, AX entity, key field and evidence.",
                note)
        };
    }

    private static int SeverityWeight(string severity) => severity.ToLowerInvariant() switch
    {
        "blocked" => 4,
        "warning" => 3,
        "review" => 2,
        "ok" => 1,
        _ => 0
    };

    private static IEnumerable<string> ExceptionKeys(ExcelAxValidationRun run) =>
        run.Rows
            .Where(r => !string.Equals(r.Insight.Severity, "ok", StringComparison.OrdinalIgnoreCase))
            .Select(r => $"{r.Insight.Category}|{r.Status}|{(string.IsNullOrWhiteSpace(r.Key) ? "row " + r.RowNumber.ToString(CultureInfo.InvariantCulture) : r.Key)}");

    private static void AppendDiffList(StringBuilder sb, string title, IReadOnlyList<string> values)
    {
        sb.AppendLine();
        sb.AppendLine(title);
        if (values.Count == 0)
        {
            sb.AppendLine("- none");
            return;
        }
        foreach (var value in values)
            sb.AppendLine("- " + value);
    }

    private static string BuildFilteredRowsAnswer(ExcelAxValidationRun run, string title, Func<ExcelAxValidationRow, bool> predicate)
    {
        var rows = run.Rows.Where(predicate).Take(40).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine(title);
        if (rows.Length == 0)
        {
            sb.AppendLine("- No matching rows in the latest run.");
            return sb.ToString().TrimEnd();
        }
        foreach (var row in rows)
            sb.AppendLine($"- Row {row.RowNumber}, key '{row.Key}': {row.Status}; {row.Insight.NextAction}");
        return sb.ToString().TrimEnd();
    }

    private static ExcelAxSafeModeCertificate BuildSafeModeCertificate(ExcelAxValidationRun run, string path) =>
        new(path,
            "This Excel + AX run executed in read-only evidence mode; no AX write, post, book or mutation command is generated by this workflow.",
            [
                "Validation uses Excel/CSV parsing, optional OData read calls and optional foreground UIA evidence capture.",
                "Generated artifacts are local CSV, JSON and Markdown evidence files.",
                "The workflow does not call AX write endpoints and does not automate posting or booking actions.",
                "All recommendations are decision support; business execution remains with the operator."
            ]);

    private static IReadOnlyList<ExcelAxValidationRun> LoadAllRuns()
    {
        if (!Directory.Exists(AppPaths.ExcelAxChecksDir))
            return [];
        return Directory.EnumerateFiles(AppPaths.ExcelAxChecksDir, "excel-ax-*.json")
            .Select(path =>
            {
                try
                {
                    return JsonSerializer.Deserialize<ExcelAxValidationRun>(File.ReadAllText(path));
                }
                catch
                {
                    return null;
                }
            })
            .Where(run => run != null)
            .Cast<ExcelAxValidationRun>()
            .OrderByDescending(run => run.CreatedUtc)
            .ToArray();
    }

    private static void AddZipEntry(ZipArchive archive, string path, List<string> included)
    {
        if (!File.Exists(path))
            return;
        archive.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Optimal);
        included.Add(path);
    }

    private static string OwnerForCategory(string category) => category switch
    {
        "ExcelDataQuality" => "Excel owner",
        "Duplicate" => "Business data owner",
        "AxConnectivity" => "AX/IT owner",
        "AxMissingMasterData" => "Master data owner",
        "ManualReview" => "Process owner",
        "Configuration" => "App setup owner",
        _ => "Operator"
    };

    private static string PriorityForSeverity(string severity) => severity.ToLowerInvariant() switch
    {
        "blocked" => "P1",
        "warning" => "P2",
        "review" => "P2",
        _ => "P3"
    };

    private static (string Field, int Confidence, string Reason) GuessAxField(string normalized, string original, ExcelAxValidationOptions options)
    {
        if (string.Equals(original, options.KeyColumn, StringComparison.OrdinalIgnoreCase))
            return ($"{options.AxEntity}.{options.AxKeyField}", 98, "Selected key column for the current AX preset.");
        if (normalized.Contains("account", StringComparison.Ordinal) || normalized.Contains("konto", StringComparison.Ordinal) || normalized.Contains("debitor", StringComparison.Ordinal) || normalized.Contains("kreditor", StringComparison.Ordinal) || normalized.Contains("customer", StringComparison.Ordinal) || normalized.Contains("vendor", StringComparison.Ordinal))
            return ($"{options.AxEntity}.AccountNum", 88, "Name looks like an AX account identifier.");
        if (normalized.Contains("item", StringComparison.Ordinal) || normalized.Contains("artikel", StringComparison.Ordinal) || normalized.Contains("product", StringComparison.Ordinal))
            return ("InventTable.ItemId", 86, "Name looks like an AX item/product identifier.");
        if (normalized.Contains("dataarea", StringComparison.Ordinal) || normalized.Contains("company", StringComparison.Ordinal) || normalized.Contains("mandant", StringComparison.Ordinal))
            return ("Common.DataAreaId", 90, "Name looks like AX company/DataArea context.");
        if (normalized.Contains("name", StringComparison.Ordinal) || normalized.Contains("firma", StringComparison.Ordinal) || normalized.Contains("bezeichnung", StringComparison.Ordinal))
            return ($"{options.AxEntity}.Name", 72, "Name-like descriptive field.");
        if (normalized.Contains("currency", StringComparison.Ordinal) || normalized.Contains("waehrung", StringComparison.Ordinal) || normalized.Contains("wahrung", StringComparison.Ordinal))
            return ("CurrencyCode", 70, "Currency context can affect company-specific checks.");
        return ("Unmapped", 25, "No strong AX field signal detected.");
    }

    private static string NormalizeToken(string value)
    {
        var normalized = value.ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);
        var sb = new StringBuilder();
        foreach (var c in normalized)
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        return sb.ToString();
    }

    private static string DuplicateToken(string value)
    {
        var token = NormalizeToken(value)
            .Replace("gmbh", "", StringComparison.Ordinal)
            .Replace("ag", "", StringComparison.Ordinal)
            .Replace("kg", "", StringComparison.Ordinal)
            .Replace("mbh", "", StringComparison.Ordinal)
            .Replace("berlin", "", StringComparison.Ordinal)
            .Replace("hamburg", "", StringComparison.Ordinal)
            .Replace("munich", "", StringComparison.Ordinal)
            .Replace("muenchen", "", StringComparison.Ordinal);
        return token.Length <= 10 ? token : token[..10];
    }

    private static (string Status, string Evidence, string Note) LookupAx(string key, ExcelAxValidationOptions options, NexusSettings settings)
    {
        if (string.IsNullOrWhiteSpace(options.AxEntity) || string.IsNullOrWhiteSpace(options.AxKeyField))
            return ("NeedsManualReview", "", "Custom OData requires AX entity and key field.");

        if (!settings.AxIntegrationEnabled)
            return ("AxUnavailable", "", "AX integration disabled in Setup.");

        if (!string.IsNullOrWhiteSpace(settings.AxODataBaseUrl))
        {
            var escaped = key.Replace("'", "''", StringComparison.Ordinal);
            var path = $"{options.AxEntity}?$filter={options.AxKeyField} eq '{escaped}'&$top=1";
            var result = Ax2012ODataClient.GetStringSync(settings, path);
            if (result.StartsWith("[ERR]", StringComparison.OrdinalIgnoreCase))
                return ("AxUnavailable", result, "OData read failed.");
            if (result.StartsWith("[SKIP]", StringComparison.OrdinalIgnoreCase))
                return ("AxUnavailable", result, "OData is not fully configured.");
            if (result.Contains(key, StringComparison.OrdinalIgnoreCase))
                return ("OK", result, "AX OData returned a matching record.");
            return ("NotFound", result, "AX OData did not return the key in the response snippet.");
        }

        if (AxClientAutomationService.TryExecute("ax.form_summary", settings, out var message))
            return ("NeedsManualReview", message, "No OData endpoint configured; foreground AX/UIA context captured for manual review.");

        return ("AxUnavailable", "", "No AX read backend available.");
    }

    private static string BuildSummary(IReadOnlyList<ExcelAxValidationRow> rows, NexusSettings settings)
    {
        var groups = rows.GroupBy(r => r.Status).OrderBy(g => g.Key).Select(g => $"{g.Key}: {g.Count()}");
        var mode = !settings.AxIntegrationEnabled ? "AX disabled"
            : !string.IsNullOrWhiteSpace(settings.AxODataBaseUrl) ? "OData read"
            : "foreground UIA fallback";
        return $"Rows: {rows.Count}; Mode: {mode}; " + string.Join("; ", groups);
    }

    private static void ExportCsv(ExcelAxValidationRun run, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("runId,rowNumber,key,status,category,severity,explanation,nextAction,evidenceHint,note,axEvidence");
        foreach (var row in run.Rows)
        {
            sb.Append(Escape(run.RunId)).Append(',')
                .Append(row.RowNumber).Append(',')
                .Append(Escape(row.Key)).Append(',')
                .Append(Escape(row.Status)).Append(',')
                .Append(Escape(row.Insight.Category)).Append(',')
                .Append(Escape(row.Insight.Severity)).Append(',')
                .Append(Escape(row.Insight.Explanation)).Append(',')
                .Append(Escape(row.Insight.NextAction)).Append(',')
                .Append(Escape(row.Insight.EvidenceHint)).Append(',')
                .Append(Escape(row.Note)).Append(',')
                .AppendLine(Escape(row.AxEvidence));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static IReadOnlyList<ExcelAxColumnProfile> BuildProfiles(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var result = new List<ExcelAxColumnProfile>();
        for (var i = 0; i < columns.Count; i++)
        {
            var nonEmpty = rows.Count(r => i < r.Count && !string.IsNullOrWhiteSpace(r[i]));
            var name = columns[i];
            var n = name.ToLowerInvariant();
            var isContext = n.Contains("dataarea", StringComparison.Ordinal)
                            || n.Contains("company", StringComparison.Ordinal)
                            || n.Contains("mandant", StringComparison.Ordinal);
            var looks = !isContext && (n.Contains("konto", StringComparison.Ordinal)
                        || n.Contains("account", StringComparison.Ordinal)
                        || n.Contains("customer", StringComparison.Ordinal)
                        || n.Contains("debitor", StringComparison.Ordinal)
                        || n.Contains("kreditor", StringComparison.Ordinal)
                        || n.Contains("vendor", StringComparison.Ordinal)
                        || n.Contains("item", StringComparison.Ordinal)
                        || n.EndsWith("id", StringComparison.Ordinal));
            result.Add(new ExcelAxColumnProfile(name, i, nonEmpty, looks));
        }
        return result;
    }

    private static List<List<string>> ReadCsv(string path, int maxRows)
    {
        var rows = new List<List<string>>();
        foreach (var line in File.ReadLines(path).Take(maxRows))
            rows.Add(ParseCsvLine(line));
        return rows;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"')
            {
                sb.Append('"');
                i++;
            }
            else if (c == '"')
            {
                quoted = !quoted;
            }
            else if ((c == ',' || c == ';' || c == '\t') && !quoted)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }

    private static List<List<string>> ReadXlsx(string path, int maxRows, out string sheetName)
    {
        using var doc = SpreadsheetDocument.Open(path, false);
        var wb = doc.WorkbookPart ?? throw new InvalidOperationException("Workbook part missing.");
        var sheet = wb.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault() ?? throw new InvalidOperationException("Workbook contains no sheets.");
        sheetName = sheet.Name?.Value ?? "Sheet1";
        var part = (WorksheetPart)wb.GetPartById(sheet.Id!);
        var shared = wb.SharedStringTablePart?.SharedStringTable;
        return part.Worksheet.Descendants<Row>()
            .Take(maxRows)
            .Select(r => ReadXlsxRow(r, shared))
            .ToList();
    }

    private static List<string> ReadXlsxRow(Row row, SharedStringTable? shared)
    {
        var cells = new List<string>();
        var expected = 1;
        foreach (var cell in row.Elements<Cell>())
        {
            var index = ColumnIndex(cell.CellReference?.Value);
            while (expected < index)
            {
                cells.Add("");
                expected++;
            }
            cells.Add(ReadCell(cell, shared));
            expected++;
        }
        return cells;
    }

    private static string ReadCell(Cell cell, SharedStringTable? shared)
    {
        var value = cell.CellValue?.InnerText ?? "";
        if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(value, out var idx))
            return shared?.ElementAtOrDefault(idx)?.InnerText ?? "";
        return value;
    }

    private static int ColumnIndex(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return 1;
        var sum = 0;
        foreach (var c in reference.TakeWhile(char.IsLetter))
            sum = (sum * 26) + char.ToUpperInvariant(c) - 'A' + 1;
        return Math.Max(sum, 1);
    }

    private static string GetFirstSheetName(string path)
    {
        try
        {
            using var doc = SpreadsheetDocument.Open(path, false);
            return doc.WorkbookPart?.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault()?.Name?.Value ?? "Sheet1";
        }
        catch
        {
            return "Sheet1";
        }
    }

    private static IReadOnlyList<string> NormalizeWidth(IReadOnlyList<string> row, int width)
    {
        var result = row.Take(width).ToList();
        while (result.Count < width)
            result.Add("");
        return result;
    }

    private static string Escape(string? value)
    {
        var v = value ?? "";
        return "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal) + "\"";
    }

    private static string Short(string? value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value ?? "" : value[..max] + "...";
}
