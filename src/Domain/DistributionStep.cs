namespace OpenSettlers.Domain;

/// <summary>
/// One step of the order in which a storage hands its goods out to requests (the program's tables at <c>0x42a313</c>,
/// <c>0x42a35d</c>, and <c>0x42a3a7</c>, <c>docs/rules/construction-materials.md</c>): the flags asking for the class <see cref="Class"/>
/// in the request word <see cref="Word"/> are served from the stock word at 6 plus <see cref="StockOffset"/>, which is the good
/// <c>StockOffset / 2 + 1</c>; an offset of -1 is the step of the food goods (1, 3, and 6).
/// </summary>
/// <param name="Word">The request word the step looks in.</param>
/// <param name="Class">The class the step looks for.</param>
/// <param name="StockOffset">The byte offset of the stock word from the first stock word, or -1.</param>
public readonly record struct DistributionStep(RequestWord Word, int Class, int StockOffset);
