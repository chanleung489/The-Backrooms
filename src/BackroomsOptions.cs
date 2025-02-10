using Menu.Remix.MixedUI;
using UnityEngine;

namespace TheBackrooms;

sealed class BackroomsOptions : OptionInterface
{
    public static Configurable<int> dangerlevel;
    public static Configurable<bool> showWarning;
    public static Configurable<bool> noclipWarp;
    public static Configurable<bool> warpOnAny;
    public static Configurable<bool> progressMeter;
    public static Configurable<int> prewarpDuration;
    public static Configurable<float> noclipMushroomChance;
    public static Configurable<int> noclipDuration;

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
        showWarning = this.config.Bind<bool>(
            key: "scaryWarning",
            defaultValue: false,
            info: new ConfigurableInfo("Show scary warning")
        );
        noclipWarp = this.config.Bind<bool>(
            key: "noclipWarp",
            defaultValue: true,
            info: new ConfigurableInfo("Warp to the Backroom when out of bounds")
        );
        warpOnAny = this.config.Bind<bool>(
            key: "warpAll",
            defaultValue: false,
            info: new ConfigurableInfo("Warp all players when anyone is out of bounds")
        );
        progressMeter = this.config.Bind<bool>(
            key: "progressMeter",
            defaultValue: true,
            info: new ConfigurableInfo("Show the warpping progress")
        );
        prewarpDuration = this.config.Bind<int>(
            key: "prewarpDuration",
            defaultValue: 5,
            info: new ConfigurableInfo(
                description: "Time spent out of bounds to start warping (seconds)",
                acceptable: new ConfigAcceptableRange<int>(1, 240)
            )
        );
        noclipMushroomChance = this.config.Bind<float>(
            key: "noclipMushroomChance",
            defaultValue: 0.5f,
            info: new ConfigurableInfo(
                description: "The chance of noclip-ing after eating mushrooms (0 for never, 1 for always)",
                acceptable: new ConfigAcceptableRange<float>(0, 1)
            )
        );
        noclipDuration = this.config.Bind<int>(
            key: "noclipDuration",
            defaultValue: 1,
            info: new ConfigurableInfo(
                description: "Duration of noclip effect from eating mushrooms (seconds)",
                acceptable: new ConfigAcceptableRange<int>(1, 8)
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

            new OpCheckBox(showWarning, new Vector2(x, y -= 36))
            {
                description = showWarning.info.description
            },
            new OpLabel(new Vector2(x + 40, y), new Vector2(240f, 30f), "Scary Warning", alignment: FLabelAlignment.Left),

            new OpCheckBox(noclipWarp, new Vector2(x, y -= 36))
            {
                description = noclipWarp.info.description
            },
            new OpLabel(new Vector2(x + 40, y), new Vector2(240f, 30f), "Noclip Warping", alignment: FLabelAlignment.Left),

            new OpCheckBox(warpOnAny, new Vector2(x, y -= 36))
            {
                description = warpOnAny.info.description
            },
            new OpLabel(new Vector2(x + 40, y), new Vector2(240f, 30f), "Warp Together", alignment: FLabelAlignment.Left),

            new OpCheckBox(progressMeter, new Vector2(x, y -= 36))
            {
                description = progressMeter.info.description
            },
            new OpLabel(new Vector2(x + 40, y), new Vector2(240f, 30f), "Warp Progress Meter", alignment: FLabelAlignment.Left),

            new OpUpdown(prewarpDuration, new Vector2(x, y -= 36), 80f)
            {
                description = prewarpDuration.info.description
            },
            new OpLabel(new Vector2(x + 110, y), new Vector2(240f, 30f), "Pre-warp Duration", alignment: FLabelAlignment.Left),

            new OpUpdown(noclipMushroomChance, new Vector2(x, y -= 36), 80f)
            {
                description = noclipMushroomChance.info.description
            },
            new OpLabel(new Vector2(x + 110, y), new Vector2(240f, 30f), "Mushroom Noclip Chance", alignment: FLabelAlignment.Left),

            new OpUpdown(noclipDuration, new Vector2(x, y -= 36), 80f)
            {
                description = noclipDuration.info.description
            },
            new OpLabel(new Vector2(x + 110, y), new Vector2(240f, 30f), "Noclip Duration", alignment: FLabelAlignment.Left),

        };

        Tabs[0].AddItems(uielements);

    }
}
