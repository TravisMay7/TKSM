namespace TKSM.Abstractions.VarStore;

public interface IVarStore
{
	IVarTxn? BeginTransaction();
	object? Get(string key);
	void Set(string key, object? value); // optional direct set
}


public interface IVarTxn : IDisposable
{
	void Set(string key, object? value);
	void Commit();
	void Rollback();
}