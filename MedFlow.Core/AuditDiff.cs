namespace MedFlow.Core;

/// <summary>Finds which scalar properties of an entity changed, returning names only (never values).</summary>
public static class AuditDiff
{
    private static readonly HashSet<string> Ignored = new() { "Id", "CreatedAt", "UpdatedAt", "IsDeleted" };

    private static IEnumerable<System.Reflection.PropertyInfo> Scalars(object entity) =>
        entity.GetType().GetProperties().Where(p =>
        {
            if (!p.CanRead || p.GetIndexParameters().Length > 0 || Ignored.Contains(p.Name)) return false;
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal)
                   || t == typeof(DateTime) || t == typeof(DateOnly) || t == typeof(Guid);
        });

    public static Dictionary<string, object?> Snapshot(object entity) =>
        Scalars(entity).ToDictionary(p => p.Name, p => p.GetValue(entity));

    public static string[] Changed(Dictionary<string, object?> before, object entity) =>
        Scalars(entity)
            .Where(p => before.TryGetValue(p.Name, out var old) && !Equals(old, p.GetValue(entity)))
            .Select(p => p.Name)
            .ToArray();
}
