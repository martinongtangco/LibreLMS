namespace LibreLms.SharedKernel;

/// <summary>Base type for anything with a stable identity. Equality is by Id, not by value.</summary>
public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; set; } = default!;

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && other.GetType() == GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
