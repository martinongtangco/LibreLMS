namespace LibreLms.Contracts.Catalog;

/// <summary>Minimal course data exposed across module boundaries.</summary>
public record CourseSummary(Guid Id, string Title);
