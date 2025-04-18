using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class BZProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var initialHyperNoblium = mixture.GetMoles(Gas.HyperNoblium);
        if (initialHyperNoblium >= 5.0f && mixture.Temperature > 20f)
            return ReactionResult.NoReaction;

        var initialNitrousOxide = mixture.GetMoles(Gas.NitrousOxide);
        var initialPlasma = mixture.GetMoles(Gas.Plasma);

        var environmentEfficiency = mixture.Volume / mixture.Pressure;
        var ratioEfficiency = Math.Min(initialNitrousOxide / initialPlasma, 1);

        var bZFormed = Math.Min(ratioEfficiency * environmentEfficiency, Math.Min(initialNitrousOxide * 4f, initialPlasma * 8f));

        if (initialNitrousOxide - bZFormed * 4f < 0 || initialPlasma - (8f - bZFormed) < 0 || bZFormed <= 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        var amountDecomposed = 0.0f;
        var nitrousOxideDecomposedFactor = Math.Max(4.0f * (initialPlasma / (initialNitrousOxide + initialPlasma) - 0.75f), 0);
        if (nitrousOxideDecomposedFactor > 0)
        {
            amountDecomposed = 4f * bZFormed * nitrousOxideDecomposedFactor;
            mixture.AdjustMoles(Gas.Oxygen, amountDecomposed);
            mixture.AdjustMoles(Gas.Nitrogen, 5f * amountDecomposed);
        }

        mixture.AdjustMoles(Gas.BZ, Math.Max(0f, bZFormed * (1.0f - nitrousOxideDecomposedFactor)));
        mixture.AdjustMoles(Gas.NitrousOxide, -4f * bZFormed);
        mixture.AdjustMoles(Gas.Plasma, -8f * bZFormed * (1.0f - nitrousOxideDecomposedFactor));

        var energyReleased = bZFormed * (Atmospherics.BZFormationEnergy + nitrousOxideDecomposedFactor * (Atmospherics.NitrousOxideDecompositionEnergy - Atmospherics.BZFormationEnergy));

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
