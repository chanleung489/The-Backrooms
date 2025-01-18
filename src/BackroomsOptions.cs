using Menu.Remix.MixedUI;
using UnityEngine;

namespace TheBackrooms;

sealed class BackroomsOptions : OptionInterface
{
    public static Configurable<int> dangerlevel;
    public static Configurable<bool> scaryWarning;
    public static Configurable<float> noclipMushroomChance;

    public BackroomsOptions()
    {
        dangerlevel = this.config.Bind<int>(
            key: "dangerlevel",
            defaultValue: 3,
            info: new ConfigurableInfo(
                description: "No threats | Creature spawns | Creature actively hunts player",
                acceptable: new ConfigAcceptableRange<int>(1, 3)
            )
        );
        scaryWarning = this.config.Bind<bool>(
            key: "scaryWarning",
            defaultValue: false,
            info: new ConfigurableInfo("Show scary warning")
        );
        noclipMushroomChance = this.config.Bind<float>(
            key: "noclipMushroomChance",
            defaultValue: 0.5f,
            info: new ConfigurableInfo(
                description: "Determines the chance of getting noclip ability after eating mushrooms",
                acceptable: new ConfigAcceptableRange<float>(0, 1)
            )
        );
    }

    public override void Initialize()
    {
        base.Initialize();

        float x = 20;
        float y = 600;

        Tabs = new OpTab[] { new OpTab(this) };

        UIelement[] uielements = new UIelement[]
        {
            new OpLabel(x, y -= 40, "The Backrooms settings", true),

            new OpSliderTick(dangerlevel, new Vector2(x += 20, y -= 36), 300)
            {
                description = dangerlevel.info.description
            },
            new OpLabel(new Vector2(x + 320, y), new Vector2(240f, 30f), "Danger Level", alignment: FLabelAlignment.Left),

            new OpCheckBox(scaryWarning, new Vector2(x, y -= 36))
            {
                description = scaryWarning.info.description
            },
            new OpLabel(new Vector2(x + 40, y), new Vector2(240f, 30f), "Scary Warning", alignment: FLabelAlignment.Left),

            new OpUpdown(noclipMushroomChance, new Vector2(x, y -= 36), 80f)
            {
                description = noclipMushroomChance.info.description
            },
            new OpLabel(new Vector2(x + 110, y), new Vector2(240f, 30f), "Mushroom Noclip chance", alignment: FLabelAlignment.Left),
        };

        Tabs[0].AddItems(uielements);

    }
}
