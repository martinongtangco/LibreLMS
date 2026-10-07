using LibreLms.Contracts.Scorm;
using LibreLms.Modules.Scorm.Domain;
using LibreLms.Modules.Scorm.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibreLms.Modules.Scorm.Application;

/// <summary>
/// Implements <see cref="IScormAttemptStats"/> (spec 058 US1): one query per call over
/// CourseAttempt rows, aggregated in memory (dev scale — a handful of orgs, dozens of
/// learners; same per-view one-query shape as the rest of DashboardService).
///
/// "Completed" = terminal SCORM 1.2 lesson statuses (completed/passed/failed), compared
/// case-insensitively: the session shim validates cmi.core.lesson_status with
/// StringComparer.OrdinalIgnoreCase, so stored values may vary in case. Research R1 in
/// specs/058-code-review-remediation/research.md records why "completed" alone is
/// insufficient (real packages report passed/failed when a passing score is defined).
/// </summary>
public class ScormAttemptStatsService(ScormDbContext context) : IScormAttemptStats
{
    private static readonly string[] TerminalCompletedStatuses = ["completed", "passed", "failed"];

    public Task<AttemptStatsSummary> GetSystemStatsAsync() => QueryAsync(studentIds: null);

    public async Task<AttemptStatsSummary> GetStatsForStudentsAsync(IEnumerable<Guid> studentIds)
    {
        var ids = (studentIds ?? []).ToList();
        if (ids.Count == 0)
            return new AttemptStatsSummary(0, 0, 0, 0.0, 0);

        return await QueryAsync(ids);
    }

    private async Task<AttemptStatsSummary> QueryAsync(List<Guid>? studentIds)
    {
        // Filter on the entity FIRST, then project: a .Where() after a client-record
        // .Select() cannot be translated to SQL by EF Core.
        IQueryable<CourseAttempt> query = context.CourseAttempts.AsNoTracking();
        if (studentIds is not null)
            query = query.Where(a => studentIds.Contains(a.StudentId));

        var rows = await query
            .Select(a => new CourseAttemptRow(a.StudentId, a.CourseId, a.Status, a.ScoreRaw))
            .ToListAsync();

        int total = rows.Count;
        var completed = rows
            .Where(r => TerminalCompletedStatuses.Contains(r.Status, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var scored = completed.Where(r => r.ScoreRaw.HasValue).ToList();

        return new AttemptStatsSummary(
            TotalAttempts: total,
            CompletedAttempts: completed.Count,
            ScoredCompletedAttempts: scored.Count,
            CompletedScoreSum: scored.Sum(r => r.ScoreRaw!.Value),
            DistinctCompletedCourses: completed.Select(r => r.CourseId).Distinct().Count());
    }

    private sealed record CourseAttemptRow(Guid StudentId, Guid CourseId, string Status, double? ScoreRaw);
}
