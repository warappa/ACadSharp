using System.Collections.Generic;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// The evaluation context: a value store used to pass values between the nodes of an
/// <see cref="EvaluationGraph"/> during evaluation.
/// <para>
/// Mirrors the ObjectARX <c>AcDbEvalContext</c> (a key → value container). A value is
/// stored under a pair of keys: the producing expression's <c>Id</c> and the output
/// <em>port name</em> (for example <c>"DisplacementX"</c> or <c>"UpdatedEndX"</c>). A
/// dependent node reads a value by the target expression's <c>Id</c> and the port name
/// from its connection.
/// </para>
/// </summary>
public class EvaluationContext
{
	private readonly Dictionary<int, Dictionary<string, double>> _values = new Dictionary<int, Dictionary<string, double>>();

	/// <summary>
	/// Stores a value for an expression under the given port name.
	/// </summary>
	/// <param name="id">The producing expression's id.</param>
	/// <param name="port">The output port name (for example "DisplacementX").</param>
	/// <param name="value">The value to store.</param>
	public void SetValue(int id, string port, double value)
	{
		if (!this._values.TryGetValue(id, out Dictionary<string, double> ports))
		{
			ports = new Dictionary<string, double>();
			this._values[id] = ports;
		}

		ports[port] = value;
	}

	/// <summary>
	/// Tries to get a value for an expression under the given port name.
	/// </summary>
	/// <param name="id">The producing expression's id.</param>
	/// <param name="port">The output port name.</param>
	/// <param name="value">The value, if found.</param>
	/// <returns>True if the value was found; otherwise false (and <paramref name="value"/> is 0).</returns>
	public bool TryGetValue(int id, string port, out double value)
	{
		if (this._values.TryGetValue(id, out Dictionary<string, double> ports) && ports.TryGetValue(port, out value))
		{
			return true;
		}

		value = 0;
		return false;
	}

	/// <summary>
	/// Checks whether a value has been stored for an expression under the given port name.
	/// </summary>
	public bool HasValue(int id, string port)
	{
		return this._values.TryGetValue(id, out Dictionary<string, double> ports) && ports.ContainsKey(port);
	}

	/// <summary>
	/// Clears all stored values.
	/// </summary>
	public void Clear()
	{
		this._values.Clear();
	}
}
