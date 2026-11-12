using OpenSettlers.Domain;

namespace OpenSettlers.MapView;

/// <summary>The carrier's key in the content set.</summary>
public sealed class CarrierKey() : OpenSettlersLook<Carrier>("carrier");

/// <summary>The woodcutter's key in the content set.</summary>
public sealed class WoodcutterKey() : OpenSettlersLook<Woodcutter>("woodcutter");

/// <summary>The stonecutter's key in the content set.</summary>
public sealed class StonecutterKey() : OpenSettlersLook<Stonecutter>("stonecutter");

/// <summary>The key of the type 1 hut's settler in the content set.</summary>
public sealed class TypeOneHutKey() : OpenSettlersLook<TypeOneHut>("type-one-hut");

/// <summary>The builder's key in the content set.</summary>
public sealed class BuilderKey() : OpenSettlersLook<Builder>("builder");

/// <summary>The forester's key in the content set.</summary>
public sealed class ForesterKey() : OpenSettlersLook<Forester>("forester");

/// <summary>The digger's key in the content set.</summary>
public sealed class DiggerKey() : OpenSettlersLook<Digger>("digger");

/// <summary>The sawmiller's key in the content set.</summary>
public sealed class SawmillerKey() : OpenSettlersLook<Sawmiller>("sawmiller");
