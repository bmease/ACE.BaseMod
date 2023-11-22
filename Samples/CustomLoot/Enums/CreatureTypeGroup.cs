﻿namespace CustomLoot.Enums;


public enum CreatureTypeGroup
{
    Popular,
    Full,
}

public static class CreatureTypeGroupHelper
{
    public static CreatureType[] SetOf(this CreatureTypeGroup type) => type switch
    {
        CreatureTypeGroup.Popular => new[]
        {
            CreatureType.Deru,
            CreatureType.Drudge,
            CreatureType.Eater,
            CreatureType.Golem,
            CreatureType.Olthoi,
            CreatureType.Shadow,
            CreatureType.Tumerok,
            CreatureType.Tusker,
            CreatureType.Undead,
            CreatureType.Virindi
        },
        CreatureTypeGroup.Full => new[]
        {
            CreatureType.AcidElemental,
            CreatureType.Aerbax,
            CreatureType.AlteredHuman,
            CreatureType.Anekshay,
            CreatureType.Apparition,
            CreatureType.Armoredillo,
            CreatureType.AunTumerok,
            CreatureType.Auroch,
            CreatureType.Banderling,
            CreatureType.BleachedRabbit,
            CreatureType.BlightedMoarsman,
            CreatureType.Bunny,
            CreatureType.Burun,
            CreatureType.Carenzi,
            CreatureType.Chicken,
            CreatureType.Chittick,
            CreatureType.Cow,
            CreatureType.Crystal,
            CreatureType.DarkSarcophagus,
            CreatureType.Deru,
            CreatureType.Device,
            CreatureType.Doll,
            CreatureType.Drudge,
            CreatureType.Eater,
            CreatureType.Elemental,
            CreatureType.Empyrean,
            CreatureType.EnchantedArms,
            CreatureType.Energy,
            CreatureType.Fae,
            CreatureType.FireElemental,
            CreatureType.Fiun,
            CreatureType.Food,
            CreatureType.FrostElemental,
            CreatureType.GearKnight,
            CreatureType.Ghost,
            CreatureType.Golem,
            CreatureType.GotrokLugian,
            CreatureType.Grievver,
            CreatureType.GrimacingRabbit,
            CreatureType.Gromnie,
            CreatureType.Gurog,
            CreatureType.Harbinger,
            CreatureType.Harvest,
            CreatureType.HeaTumerok,
            CreatureType.HollowMinion,
            CreatureType.Hopeslayer,
            CreatureType.Human,
            CreatureType.Idol,
            CreatureType.Knathtead,
            CreatureType.LightningElemental,
            CreatureType.Lugian,
            CreatureType.Margul,
            CreatureType.Marionette,
            CreatureType.Mattekar,
            CreatureType.Merwart,
            CreatureType.Mite,
            CreatureType.Moar,
            CreatureType.Moarsman,
            CreatureType.Monouga,
            CreatureType.Mosswart,
            CreatureType.Mukkir,
            CreatureType.Mumiyah,
            CreatureType.NastyRabbit,
            CreatureType.Niffis,
            CreatureType.Olthoi,
            CreatureType.OlthoiLarvae,
            CreatureType.ParadoxOlthoi,
            CreatureType.Penguin,
            CreatureType.PhyntosWasp,
            CreatureType.Rabbit,
            CreatureType.Rat,
            CreatureType.Reedshark,
            CreatureType.Remoran,
            CreatureType.Rockslide,
            CreatureType.Ruschk,
            CreatureType.Scarecrow,
            CreatureType.Sclavus,
            CreatureType.Shadow,
            CreatureType.ShallowsShark,
            CreatureType.Shreth,
            CreatureType.Simulacrum,
            CreatureType.Siraluun,
            CreatureType.Skeleton,
            CreatureType.Sleech,
            CreatureType.Slithis,
            CreatureType.Snowman,
            CreatureType.Statue,
            CreatureType.Swarm,
            CreatureType.Target,
            CreatureType.Thrungus,
            CreatureType.Touched,
            CreatureType.Tumerok,
            CreatureType.Tusker,
            CreatureType.Undead,
            CreatureType.Unknown,
            CreatureType.Ursuin,
            CreatureType.ViamontianKnight,
            CreatureType.Virindi,
            CreatureType.Wall,
            CreatureType.Wisp,
            CreatureType.Zefir,
        },
        _ => throw new NotImplementedException(),
    };
}
