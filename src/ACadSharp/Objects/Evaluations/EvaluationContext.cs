using System;
using System.Collections.Generic;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// The shared, mutable state that flows through a block's evaluation graph.
/// <para>
/// Holds one <see cref="EvaluationValue"/> per (node id, port name) pair. When a node is evaluated,
/// it reads its input connections from this context and writes its result back, so downstream
/// nodes can consume it.
/// </para>
/// <para>
/// The value is held in its full shape (<see cref="EvaluationValue"/> — scalar, point, 2D point,
/// string, integer, character, or object id), so a text column (for example a lookup table's state
/// name) can flow through the graph as a string rather than being coerced to a number.
/// </para>
/// </summary>
public sealed class EvaluationContext
{
	private readonly Dictionary<int, Dictionary<string, EvaluationValue>> _values = new();

	/// <summary>
	/// Stores the value of the named port on the given node.
	/// </summary>
	public void SetValue(int id, string name, EvaluationValue value)
	{
		if (!_values.TryGetValue(id, out var ports))
		{
			ports = _values[id] = new Dictionary<string, EvaluationValue>();
		}

		ports[name] = value;
	}

	/// <summary>
	/// Stores a scalar value for the named port on the given node.
	/// </summary>
	public void SetValue(int id, string name, double value) => this.SetValue(id, name, EvaluationValue.FromDouble(value));

	/// <summary>
	/// Stores a string value for the named port on the given node.
	/// </summary>
	public void SetValue(int id, string name, string value) => this.SetValue(id, name, EvaluationValue.FromString(value));

	/// <summary>
	/// Reads the full value of the named port on the given node.
	/// </summary>
	public bool TryGetValue(int id, string name, out EvaluationValue value)
	{
		if (_values.TryGetValue(id, out var ports) && ports.TryGetValue(name, out var v))
		{
			value = v;
			return true;
		}

		value = EvaluationValue.None;
		return false;
	}

	/// <summary>
	/// Reads the scalar of the named port on the given node.
	/// <para>
	/// Returns <c>false</c> when the port is absent <em>or</em> holds a non-scalar value (for
	/// example a string). Use <see cref="TryGetValue(int, string, out EvaluationValue)"/> to inspect
	/// the full shape.
	/// </para>
	/// </summary>
	public bool TryGetValue(int id, string name, out double value)
	{
		if (this.TryGetValue(id, name, out EvaluationValue v) && v.DoubleValue is { } d)
		{
			value = d;
			return true;
		}

		value = 0.0;
		return false;
	}

	/// <summary>
	/// Reads the string of the named port on the given node.
	/// <para>
	/// Returns <c>false</c> when the port is absent <em>or</em> holds a non-string value. Use
	/// <see cref="TryGetValue(int, string, out EvaluationValue)"/> to inspect the full shape.
	/// </summary>
	public bool TryGetValue(int id, string name, out string value)
	{
		if (this.TryGetValue(id, name, out EvaluationValue v) && v.StringValue is { } s)
		{
			value = s;
			return true;
		}

		value = null;
		return false;
	}

	/// <summary>
	/// Whether the named port on the given node has been set.
	/// </summary>
	public bool HasValue(int id, string name) =>
		_values.TryGetValue(id, out var ports) && ports.ContainsKey(name);

	/// <summary>
	/// Clears all stored values (for example between evaluations of the same block).
	/// </summary>
	public void Clear() => _values.Clear();
}
