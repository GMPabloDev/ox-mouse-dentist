namespace MouseDent.Api.Tests;

/// <summary>Reloj que la prueba adelanta a voluntad para comprobar la caducidad.</summary>
internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; private set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan delta) => Now += delta;
}
