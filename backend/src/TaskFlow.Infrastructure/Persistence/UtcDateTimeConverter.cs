using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
  a => a.Kind == DateTimeKind.Local ? a.ToUniversalTime() : DateTime.SpecifyKind(a, DateTimeKind.Utc),
  a => DateTime.SpecifyKind(a, DateTimeKind.Utc));
