namespace TKSM.Abstractions.Scheduling;

public readonly record struct JobId(long Value)
{
	public override string ToString() => Value.ToString();
}
