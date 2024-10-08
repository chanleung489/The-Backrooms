using Menu.Remix.MixedUI;
using UnityEngine;

namespace TheBackrooms;

sealed class BackroomsOptions : OptionInterface
{
    public static Configurable<int> dangerlevel;
    public static Configurable<bool> scaryWarning;

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

            new OpLabel(new Vector2(x + 40, y -= 30), Vector2.zero, "Danger Level"),
            new OpSliderTick(dangerlevel, new Vector2(x + 110, y - 6), 300)
            {
                description = dangerlevel.info.description
            },

            new OpLabel(new Vector2(x + 40, y -= 30), Vector2.zero, "Scary Warning"),
            new OpCheckBox(scaryWarning, new Vector2(x + 100, y - 4))
            {
                description = scaryWarning.info.description
            },

        };

        Tabs[0].AddItems(uielements);

    }
}