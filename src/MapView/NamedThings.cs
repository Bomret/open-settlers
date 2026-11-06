using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The aliases and marks of one play run (<c>CONTEXT.md</c>, ADR 0011) and the steps that use them: <c>name</c>, <c>mark</c>,
/// <c>expect &lt;alias&gt;.&lt;field&gt;</c>, <c>expect-event</c>, <c>expect-gone</c>, and <c>events</c>. Each returns null when it holds
/// and else the problem. Every one of them fails in the map view, which has no game.
/// </summary>
public sealed class NamedThings
{
    /// <summary>The fields an <c>expect</c> on a named thing knows: the list fields, the fields of an event's state, and the links.</summary>
    public static readonly IReadOnlyList<string> Fields =
    [
        "id",
        "flag-id",
        "worker-id",
        "x",
        "y",
        "slot",
        "job",
        "state",
        "player",
        "mode",
        "target",
        "next",
        "heading",
        "frame",
        "timer",
        "stamp",
        "type",
        "constructing",
        "status",
        "flag",
        "worker",
        "fill",
        "progress",
        .. Enumerable.Range(0, 6).Select(d => $"road{d}"),
        .. Enumerable.Range(0, 8).Select(p => $"good{p}"),
    ];

    readonly Dictionary<string, string> _ids = [];
    readonly Dictionary<string, (long Tick, int Events)> _marks = [];

    /// <summary>Whether <paramref name="field"/> is one an <c>expect</c> on a named thing knows.</summary>
    public static bool IsField(string field) => Fields.Contains(field);

    /// <summary>Whether the field holds a thing ID, which only <c>=</c> compares.</summary>
    public static bool IsLink(string field) => field is "id" or "flag-id" or "worker-id";

    /// <summary>Whether the text is a thing ID as <c>ThingId.ToString()</c> writes it: <c>settler#12.3</c>, <c>building#…</c>, or <c>flag#…</c>.</summary>
    public static bool IsThingId(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(text, @"^(settler|building|flag)#[0-9]+\.[0-9]+$");

    /// <summary>Whether the text can be an alias: letters, digits, <c>-</c>, and <c>_</c>.</summary>
    public static bool IsAliasText(string text) =>
        text.Length > 0 && text.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    /// <summary>A number as <c>expect</c> values read it: decimal, <c>0x</c> hex, or <c>true</c> and <c>false</c>.</summary>
    public static bool Numeric(string text, out double value)
    {
        value = 0;
        switch (text)
        {
            case "true":
                value = 1;

                return true;
            case "false":
                return true;
        }

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            var ok = PlayScript.Whole(text, out var hex);
            value = hex;

            return ok;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value);
    }

    static string? Refusal(string step, PlayState state) =>
        state.Ticks is null || state.Game is null ? $"{step}: the map view has no named things, use --game-view" : null;

    static string IdOf(Thing thing) => thing.Values.First(v => v.Field == "id").Value;

    static ThingId ParseId(string text)
    {
        var hash = text.IndexOf('#');
        var dot = text.IndexOf('.', hash);

        return new ThingId(
            text[..hash],
            int.Parse(text[(hash + 1)..dot], CultureInfo.InvariantCulture),
            int.Parse(text[(dot + 1)..], CultureInfo.InvariantCulture)
        );
    }

    static Thing? Live(PlayState state, string id) =>
        (state.Things ?? []).FirstOrDefault(t => t.Values.Any(v => v.Field == "id" && v.Value == id));

    // The tick and cause of the thing's last event: where it went, as far as the events tell.
    static string Went(PlayState state, string id)
    {
        var last = state.Game!.EventsOf(ParseId(id)).LastOrDefault();

        return last is null ? "gone, it has no event" : $"gone at tick {last.Tick}, cause {last.Cause}";
    }

    /// <summary>Runs a <c>name</c> step: saves the ID of the one thing at the node that matches.</summary>
    public string? Run(NameStep step, PlayState state)
    {
        var text = $"name {step.Alias} {step.Column} {step.Row} {step.What.Text}";
        if (Refusal(text, state) is { } refused)
            return refused;
        if (!MapSize.TryFromValue(state.Size, out var map, out _))
            return $"{text}: the map size {state.Size} is not one the original offers";
        if (step.Column >= map.Width || step.Row >= map.Height)
            return $"{text}: the node is outside the {map.Width} by {map.Height} map";

        var here = (state.Things ?? []).Where(t => t.X == step.Column && t.Y == step.Row).ToList();
        var match = here.Where(step.What.Matches).ToList();
        var held = here.Count == 0 ? "nothing" : string.Join("; ", here.Select(t => t.Line()));
        if (match.Count != 1)
            return $"{text} failed: {(match.Count == 0 ? "no thing matches" : "several things match")}, the node holds {held}";
        if (!match[0].Values.Any(v => v.Field == "id"))
            return $"{text} failed: the {match[0].Kind} has no thing ID, the node holds {held}";

        _ids[step.Alias] = IdOf(match[0]);

        return null;
    }

    /// <summary>Runs a <c>mark</c> step: saves the tick and the events so far under the label.</summary>
    public string? Run(MarkStep step, PlayState state)
    {
        if (Refusal($"mark {step.Label}", state) is { } refused)
            return refused;

        _marks[step.Label] = (state.Ticks!.Value, state.Game!.Events.Count);

        return null;
    }

    /// <summary>Runs an <c>expect</c> step on a named thing.</summary>
    public string? Run(ExpectThingStep step, PlayState state)
    {
        var at =
            step.Mark is not null ? "@" + step.Mark
            : step.Tick is { } t ? $"@{t}"
            : "";
        var text = $"expect {step.Alias}.{step.Field}{at} {step.Op} {step.Value}";
        if (Refusal(text, state) is { } refused)
            return refused;

        var id = _ids[step.Alias];
        var link = IsLink(step.Field);
        string actual;
        if (at.Length == 0)
        {
            if (Live(state, id) is not { } thing)
                return $"{text} failed: {step.Alias} ({id}) is {Went(state, id)}";

            var value = step.Field switch
            {
                "x" => thing.X.ToString(CultureInfo.InvariantCulture),
                "y" => thing.Y.ToString(CultureInfo.InvariantCulture),
                _ => thing.Values.FirstOrDefault(v => v.Field == step.Field).Value,
            };
            if (value is null)
                return $"{text} failed: {step.Alias} ({id}) lists no {step.Field}: {thing.Line()}";

            actual = value;
        }
        else
        {
            if (link)
                return $"{text} failed: {step.Field} shows only now, not at a mark or tick";

            ThingState? past;
            if (step.Mark is not null)
            {
                var (_, events) = _marks[step.Mark];
                past = state.Game!.Events.All.Take(events).LastOrDefault(e => e.Thing == ParseId(id))?.State;
                if (past is null)
                    return $"{text} failed: {step.Alias} ({id}) had no event at mark {step.Mark}";
            }
            else
            {
                var (found, refusal) = state.Game!.StateAt(ParseId(id), step.Tick!.Value);
                if (found is null)
                    return $"{text} failed: {refusal}";

                past = found;
            }

            if (past.Get(step.Field) is not { } number)
                return $"{text} failed: the state of {step.Alias} ({id}) has no {step.Field}: {past}";

            actual = number.ToString(CultureInfo.InvariantCulture);
        }

        bool holds;
        if (link)
            holds = actual == (_ids.GetValueOrDefault(step.Value) ?? step.Value);
        else
        {
            Numeric(actual, out var have);
            holds = PlayState.Compare(have, step.Number, step.Op);
        }

        return holds ? null : $"{text} failed: actual {actual}";
    }

    /// <summary>Runs an <c>expect-event</c> step.</summary>
    public string? Run(ExpectEventStep step, PlayState state)
    {
        var text = $"expect-event {step.Alias} {step.Type}{(step.Cause is null ? "" : " cause " + step.Cause)}";
        if (Refusal(text, state) is { } refused)
            return refused;

        var events = state.Game!.EventsOf(ParseId(_ids[step.Alias]));
        if (events.Any(e => e.Type == step.Type && (step.Cause is null || e.Cause.Kind == step.Cause)))
            return null;

        return $"{text} failed: {step.Alias} ({_ids[step.Alias]}) has the events {(events.Count == 0 ? "none" : string.Join(", ", events.Select(e => $"{e.Type} (cause {e.Cause})")))}";
    }

    /// <summary>Runs an <c>expect-gone</c> step: passes when no thing in the game has the alias's ID.</summary>
    public string? Run(ExpectGoneStep step, PlayState state)
    {
        var text = $"expect-gone {step.Alias}";
        if (Refusal(text, state) is { } refused)
            return refused;

        return Live(state, _ids[step.Alias]) is null
            ? null
            : $"{text} failed: {step.Alias} ({_ids[step.Alias]}) is live";
    }

    /// <summary>Runs an <c>events</c> step: writes one line for each event of the thing to <c>&lt;name&gt;.events.txt</c>.</summary>
    public string? Run(EventsStep step, PlayState state, string outputFolder)
    {
        if (Refusal($"events {step.Alias} {step.Name}", state) is { } refused)
            return refused;

        var text = new StringBuilder();
        foreach (var e in state.Game!.EventsOf(ParseId(_ids[step.Alias])))
        {
            text.Append($"tick={e.Tick} type={e.Type} cause={e.Cause}");
            if (e.Other is not null)
                text.Append($" other={e.Other}");
            text.Append(' ').Append(e.State).Append('\n');
        }

        Directory.CreateDirectory(outputFolder);
        File.WriteAllText(Path.Combine(outputFolder, step.Name + ".events.txt"), text.ToString());

        return null;
    }
}
