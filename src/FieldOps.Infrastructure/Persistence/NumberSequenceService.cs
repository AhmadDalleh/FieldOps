using FieldOps.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Infrastructure.Persistence;

public sealed class NumberSequenceService(AppDbContext db) : INumberSequence
{
    public async Task<long> NextAsync(string name, CancellationToken ct)
    {
        // The row lock taken by the upsert is held until the surrounding transaction ends, so numbers are gap-free.
        var values = await db.Database.SqlQuery<long>($"""
            INSERT INTO number_sequences (name, next_value) VALUES ({name}, 2)
            ON CONFLICT (name) DO UPDATE SET next_value = number_sequences.next_value + 1
            RETURNING next_value - 1 AS "Value"
            """).ToListAsync(ct);
        return values[0];
    }
}
