namespace Advancemint;

public record Achievement(string Id, string Title, string Desc, string Icon, Func<Signals, bool> Cond, string Category);

public static class Achievements
{
    private static bool H(Signals s, int a, int b) => s.Now.Hour >= a && s.Now.Hour < b;

    public static readonly List<Achievement> All = new()
    {
        // ---- Time ----
        new("first_run",   "Taking Inventory",   "Open Advancemint for the first time",     "📖", s => true, "Time"),
        new("welcome_back", "Welcome Back",      "Come back on a new day",                  "📅", s => s.FreshDay, "Time"),
        new("veteran",     "Veteran",            "Launch the app 5 times",                  "🎖️", s => s.Sessions >= 5, "Time"),
        new("night_owl",   "Night Owl",          "Be up and about after 2 AM",              "🦉", s => H(s, 2, 5), "Time"),
        new("all_nighter", "All-Nighter",        "Still going between 4 and 6 AM",          "🌙", s => H(s, 4, 6), "Time"),
        new("early_bird",  "Early Bird",         "Beat the sunrise, up before 6 AM",        "🐦", s => H(s, 5, 6), "Time"),
        new("witching",    "The Witching Hour",  "Be active at midnight",                   "🕛", s => s.Now.Hour == 0, "Time"),
        new("lunch",       "Lunch Break",        "Active around noon",                      "🍔", s => s.Now.Hour == 12, "Time"),
        new("tgif",        "T.G.I.F.",           "Friday evening on the PC",                "🍻", s => s.Now.DayOfWeek == DayOfWeek.Friday && s.Now.Hour >= 17, "Time"),
        new("weekend",     "Weekend Warrior",    "Use your PC on a weekend",                "🎮", s => s.Now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday, "Time"),
        new("marathon",    "Marathon",           "Keep Advancemint running 3 hours",        "🏃", s => s.AppUptimeMin >= 180, "Time"),
        new("ultramarathon","Ultramarathon",     "8 hours of uptime. Go to bed.",           "🛌", s => s.AppUptimeMin >= 480, "Time"),
        new("new_year",    "New Year, New Me",   "Be on your PC on January 1st",            "🎆", s => s.Now.Month == 1 && s.Now.Day == 1, "Time"),
        new("spooky",      "Spooky Scary",       "Boot up on Halloween",                    "🎃", s => s.Now.Month == 10 && s.Now.Day == 31, "Time"),
        new("hohoho",      "Ho Ho Ho",           "Christmas at the keyboard",               "🎄", s => s.Now.Month == 12 && s.Now.Day == 25, "Time"),

        // ---- System ----
        new("unplugged",   "Unplugged",          "Run on battery power",                    "🔌", s => s.HasBattery && !s.Charging, "System"),
        new("low_batt",    "Danger Zone",        "Let the battery drop to 15%",             "🪫", s => s.HasBattery && s.Battery is >= 0 and <= 15, "System"),
        new("full_batt",   "Fully Charged",      "Top the battery off to 100%",             "🔋", s => s.HasBattery && s.Battery >= 100, "System"),
        new("overclock",   "Overclocked",        "Push CPU usage past 90%",                 "🔥", s => s.Cpu >= 90, "System"),
        new("mem_hog",     "Memory Hog",         "Push RAM usage past 90%",                 "🧠", s => s.RamPercent >= 90, "System"),
        new("meltdown",    "System Meltdown",    "CPU and RAM both over 85% at once",       "💥", s => s.Cpu >= 85 && s.RamPercent >= 85, "System"),
        new("disk_full",   "Spring Cleaning",    "Under 15 GB free on your system drive",   "💾", s => s.FreeDiskBytes > 0 && s.FreeDiskBytes < 15L * 1024 * 1024 * 1024, "System"),
        new("dual_mon",    "Double Vision",      "Plug in a second monitor",                "🖥️", s => s.Monitors >= 2, "System"),
        new("tri_mon",     "Command Center",     "Run three or more monitors",              "🛰️", s => s.Monitors >= 3, "System"),
        new("offline",     "Gone Dark",          "Disconnect from the internet",            "📡", s => !s.Network, "System"),
        new("afk",         "Be Right Back",      "Step away for 5 minutes",                 "💤", s => s.IdleMs > 5 * 60_000, "System"),
        new("dead",        "Did You Die?",       "Go idle for 30 minutes",                  "⚰️", s => s.IdleMs > 30 * 60_000, "System"),
        new("busy",        "Organized Chaos",    "Have 15+ windows open at once",           "🗂️", s => s.WindowCount >= 15, "System"),
        new("hoarder",     "Window Hoarder",     "Have 25+ windows open at once",           "🪟", s => s.WindowCount >= 25, "System"),

        // ---- Apps ----
        new("browser",     "Surf's Up",          "Open a web browser",                      "🌊", s => s.Proc("zen", "firefox", "chrome", "msedge", "brave", "opera"), "Apps"),
        new("zen",         "Zen Master",         "Open Zen Browser",                        "🧘", s => s.Proc("zen"), "Apps"),
        new("discord",     "Social Butterfly",   "Open Discord",                            "💬", s => s.Proc("discord", "discordptb", "discordcanary", "vesktop"), "Apps"),
        new("spotify",     "Turn It Up",         "Open Spotify",                            "🎧", s => s.Proc("spotify"), "Apps"),
        new("cider",       "Apple of My Eye",    "Open Cider",                              "🍎", s => s.Proc("cider"), "Apps"),
        new("code",        "Hello, World",       "Open VS Code",                            "👨‍💻", s => s.Proc("code"), "Apps"),
        new("terminal",    "Hacker Voice",       "Open a terminal",                         "⌨️", s => s.Proc("windowsterminal", "cmd", "powershell", "pwsh"), "Apps"),
        new("steam",       "Game On",            "Open Steam",                              "🎯", s => s.Proc("steam"), "Apps"),
        new("bonelab",     "Museum Trip",        "Launch BONELAB",                          "🦴", s => s.Proc("bonelab"), "Apps"),
        new("unity",       "The Modder",         "Open Unity",                              "🧩", s => s.Proc("unity", "unityhub"), "Apps"),
        new("blender",     "Blend Master",       "Open Blender",                            "🥤", s => s.Proc("blender"), "Apps"),
        new("obs",         "On Air",             "Open OBS",                                "🔴", s => s.Proc("obs64", "obs32", "obs"), "Apps"),
        new("paint",       "Fine Art",           "Open Paint",                              "🎨", s => s.Proc("mspaint"), "Apps"),
        new("notepad",     "Note To Self",       "Open Notepad",                            "📝", s => s.Proc("notepad"), "Apps"),
        new("taskmgr",     "Diamonds!",          "Open Task Manager",                       "💎", s => s.Proc("taskmgr"), "Apps"),
        new("vlc",         "Roll Film",          "Open VLC",                                "🎬", s => s.Proc("vlc"), "Apps"),
        new("multitask",   "Multitasker",        "Use 6 different apps in one session",     "🤹", s => s.AppsSeen.Count >= 6, "Apps"),
        new("busybody",    "Jack of All Tabs",   "Use 12 different apps in one session",    "🎪", s => s.AppsSeen.Count >= 12, "Apps"),

        // ---- Media ----
        new("first_song",  "Now Playing",        "Play your first song",                    "🎵", s => s.SongsEver >= 1, "Media"),
        new("dj",          "Resident DJ",        "Play 10 different songs",                  "🎚️", s => s.DistinctSongs >= 10, "Media"),
        new("audiophile",  "Audiophile",         "Play 50 songs total",                     "🎼", s => s.SongsEver >= 50, "Media"),
        new("on_repeat",   "On Repeat",          "Play 5 songs in one session",             "🔁", s => s.SongsSession >= 5, "Media"),
        new("one_more",    "Just One More Song", "Playing music after midnight",            "🌃", s => s.MusicPlaying && H(s, 0, 4), "Media"),

        // ---- Meta ----
        new("ach_5",       "Getting Started",    "Unlock 5 achievements",                   "⭐", s => s.Unlocked >= 5, "Meta"),
        new("ach_15",      "Achievement Hunter", "Unlock 15 achievements",                  "🏆", s => s.Unlocked >= 15, "Meta"),
        new("ach_30",      "Completionist",      "Unlock 30 achievements",                  "👑", s => s.Unlocked >= 30, "Meta"),
        new("ach_all",     "The End?",           "Unlock every other achievement",          "🌌", s => s.Unlocked >= All.Count - 1, "Meta"),
    };
}
