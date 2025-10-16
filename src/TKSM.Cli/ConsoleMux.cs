using System.Text;

namespace TKSM.Host.Cli;

// Minimal console multiplexer: live output + interactive input on one line.
public sealed class ConsoleMux : IDisposable
{
	private readonly object _gate = new();
	private readonly string _prompt;
	private readonly StringBuilder _input = new();
	private bool _running;
	private Task? _reader;

	public ConsoleMux(string prompt = "> ")
	{
		_prompt = prompt;
		Console.TreatControlCAsInput = true; // we handle Ctrl+C in-loop
	}

	// Start reading keys. Each full line is sent to onCommand.
	public void Start(Func<string, Task> onCommand)
	{
		if (_running) return;
		_running = true;
		RedrawInput();

		_reader = Task.Run(async () =>
		{
			while (_running)
			{
				var key = Console.ReadKey(intercept: true);

				if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
				{
					// Pass Ctrl+C up to host (don’t exit process here).
					WriteLine("^C");
					_input.Clear();
					RedrawInput();
					continue;
				}

				switch (key.Key)
				{
					case ConsoleKey.Enter:
						{
							var cmd = _input.ToString();
							_input.Clear();
							// Move to a new line for command echo and processing
							lock (_gate)
							{
								Console.WriteLine();
							}
							await onCommand(cmd);
							RedrawInput();
							break;
						}

					case ConsoleKey.Backspace:
						if (_input.Length > 0) { _input.Remove(_input.Length - 1, 1); RedrawInput(); }
						break;

					case ConsoleKey.Escape:
						_input.Clear(); RedrawInput();
						break;

					case ConsoleKey.LeftArrow:
					case ConsoleKey.RightArrow:
					case ConsoleKey.UpArrow:
					case ConsoleKey.DownArrow:
						// keep it simple: ignore editing/navigation in fallback mode
						break;

					default:
						if (!char.IsControl(key.KeyChar))
						{
							_input.Append(key.KeyChar);
							RedrawInput();
						}
						break;
				}
			}
		});
	}

	// Thread-safe write that doesn't break the input line.
	public void WriteLine(string line)
	{
		lock (_gate)
		{
			// Clear the input line
			Console.Write('\r');
			Console.Write(new string(' ', _prompt.Length + _input.Length));
			Console.Write('\r');

			// Write the message
			Console.WriteLine(line);

			// Repaint the prompt + current input
			Console.Write(_prompt);
			Console.Write(_input.ToString());
		}
	}

	private void RedrawInput()
	{
		lock (_gate)
		{
			Console.Write('\r');
			Console.Write(new string(' ', Console.BufferWidth - 1)); // soft clear current line
			Console.Write('\r');
			Console.Write(_prompt);
			Console.Write(_input.ToString());
		}
	}

	public void Dispose()
	{
		_running = false;
		try { _reader?.Wait(50); } catch { }
	}
}
