using System.Collections.ObjectModel;

namespace MafiaManagerApp;

public class Player
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public bool RoleSeen { get; set; }
    public bool IsAlive { get; set; } = true;
    public bool UsedChallengeThisRound { get; set; }
    public override string ToString() => $"{Number}. {Name}";
}

public partial class MainPage : ContentPage
{
    readonly Random _random = new();
    readonly ObservableCollection<Player> _players = new();
    readonly ObservableCollection<string> _roles = new();
    readonly List<string> _remainingRoles = new();
    int _playerCount = 10;
    int _currentRevealIndex;
    int _currentSpeakerIndex;
    int _mainSeconds = 45;
    int _challengeSeconds = 25;
    CancellationTokenSource? _timerCts;

    readonly Color Bg = Color.FromArgb("#F6F7FB");
    readonly Color Card = Colors.White;
    readonly Color Primary = Color.FromArgb("#4F46E5");
    readonly Color Dark = Color.FromArgb("#111827");
    readonly Color Muted = Color.FromArgb("#6B7280");

    public MainPage()
    {
        Title = "Mafia Manager";
        BackgroundColor = Bg;
        LoadDefaultRoles();
        ShowHome();
    }

    void LoadDefaultRoles()
    {
        _roles.Clear();
        foreach (var r in new[] { "Godfather", "Mafia", "Mafia", "Doctor", "Detective", "Sniper", "Mayor", "Professional", "Citizen", "Citizen", "Citizen", "Citizen" })
            _roles.Add(r);
    }

    Button Btn(string text, Color? bg = null, Color? fg = null) => new()
    {
        Text = text,
        BackgroundColor = bg ?? Primary,
        TextColor = fg ?? Colors.White,
        Margin = new Thickness(0, 5),
        HeightRequest = 56
    };

    Label H(string text, int size = 26) => new()
    {
        Text = text,
        FontSize = size,
        FontAttributes = FontAttributes.Bold,
        TextColor = Dark,
        HorizontalTextAlignment = TextAlignment.Center
    };

    Border CardBox(View content) => new()
    {
        StrokeShape = new RoundRectangle { CornerRadius = 24 },
        Stroke = Color.FromArgb("#E5E7EB"),
        BackgroundColor = Card,
        Padding = 18,
        Margin = new Thickness(12, 8),
        Content = content
    };

    void Set(View view) => Content = new ScrollView { Content = view };

    void ShowHome()
    {
        var start = Btn("New Game");
        start.Clicked += (_, _) => ShowSetup();
        Set(new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 14,
            Children =
            {
                H("Mafia Game Manager", 30),
                new Label { Text="Role spinner, speaker turns, challenge timer, voting, night/day manager.", FontSize=18, TextColor=Muted, HorizontalTextAlignment=TextAlignment.Center },
                CardBox(new VerticalStackLayout
                {
                    Children =
                    {
                        start,
                        new Label { Text="Designed with big buttons and readable text for live game use.", FontSize=16, TextColor=Muted, HorizontalTextAlignment=TextAlignment.Center }
                    }
                })
            }
        });
    }

    void ShowSetup()
    {
        var countEntry = new Entry { Text = _playerCount.ToString(), Keyboard = Keyboard.Numeric, Placeholder = "Number of players" };
        var rolesList = new VerticalStackLayout { Spacing = 6 };
        void RefreshRoles()
        {
            rolesList.Children.Clear();
            foreach (var role in _roles.ToList())
            {
                var remove = Btn("X", Color.FromArgb("#FEE2E2"), Color.FromArgb("#991B1B"));
                remove.WidthRequest = 54;
                remove.Clicked += (_, _) => { _roles.Remove(role); RefreshRoles(); };
                rolesList.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(64) },
                    Children = { new Label { Text = role, FontSize = 19, TextColor = Dark, VerticalTextAlignment = TextAlignment.Center }, remove }
                });
            }
        }
        RefreshRoles();

        var roleEntry = new Entry { Placeholder = "Add new role" };
        var addRole = Btn("Add Role", Color.FromArgb("#E0E7FF"), Primary);
        addRole.Clicked += (_, _) => { if (!string.IsNullOrWhiteSpace(roleEntry.Text)) { _roles.Add(roleEntry.Text.Trim()); roleEntry.Text = ""; RefreshRoles(); } };

        var next = Btn("Continue to Players");
        next.Clicked += async (_, _) =>
        {
            if (!int.TryParse(countEntry.Text, out _playerCount) || _playerCount < 3) { await DisplayAlert("Error", "Player count must be 3 or more.", "OK"); return; }
            if (_roles.Count < _playerCount) { await DisplayAlert("Error", "Roles must be equal or more than players.", "OK"); return; }
            ShowPlayers();
        };

        Set(new VerticalStackLayout
        {
            Padding = 16,
            Children =
            {
                H("Game Setup"),
                CardBox(new VerticalStackLayout { Spacing=10, Children={ new Label{Text="Number of players", FontSize=17, TextColor=Muted}, countEntry } }),
                CardBox(new VerticalStackLayout { Spacing=10, Children={ H("Roles",22), rolesList, roleEntry, addRole } }),
                next
            }
        });
    }

    void ShowPlayers()
    {
        var entries = new List<Entry>();
        var stack = new VerticalStackLayout { Spacing = 8 };
        for (int i = 1; i <= _playerCount; i++)
        {
            var e = new Entry { Placeholder = $"Player {i} name", Text = $"Player {i}" };
            entries.Add(e);
            stack.Children.Add(e);
        }
        var start = Btn("Start Role Spinner");
        start.Clicked += (_, _) =>
        {
            _players.Clear();
            for (int i = 0; i < entries.Count; i++) _players.Add(new Player { Number = i + 1, Name = string.IsNullOrWhiteSpace(entries[i].Text) ? $"Player {i + 1}" : entries[i].Text.Trim() });
            _remainingRoles.Clear();
            foreach (var r in _roles.OrderBy(_ => _random.Next()).Take(_playerCount)) _remainingRoles.Add(r);
            _currentRevealIndex = 0;
            ShowSpinner();
        };
        Set(new VerticalStackLayout { Padding=16, Children={ H("Players"), CardBox(stack), start } });
    }

    void ShowSpinner()
    {
        if (_currentRevealIndex >= _players.Count) { ShowGameDashboard(); return; }
        var p = _players[_currentRevealIndex];
        var roleLabel = H("Tap SPIN to get role", 24);
        roleLabel.TextColor = Primary;
        var spin = Btn("🎡 SPIN ROLE", Primary);
        var hideNext = Btn("Hide & Next Player", Color.FromArgb("#111827"));
        hideNext.IsVisible = false;
        spin.Clicked += async (_, _) =>
        {
            spin.IsEnabled = false;
            for (int i = 0; i < 14; i++)
            {
                roleLabel.Text = _remainingRoles[_random.Next(_remainingRoles.Count)];
                await Task.Delay(80 + i * 10);
            }
            var index = _random.Next(_remainingRoles.Count);
            p.Role = _remainingRoles[index];
            p.RoleSeen = true;
            _remainingRoles.RemoveAt(index);
            roleLabel.Text = p.Role;
            await TryBeep();
            hideNext.IsVisible = true;
        };
        hideNext.Clicked += (_, _) => { _currentRevealIndex++; ShowSpinner(); };
        Set(new VerticalStackLayout
        {
            Padding=18,
            Spacing=14,
            Children=
            {
                H($"Role for {p.Name}"),
                CardBox(new VerticalStackLayout{ Spacing=16, Children={ new Label{Text="Give phone to this player only.", FontSize=18, TextColor=Muted, HorizontalTextAlignment=TextAlignment.Center}, roleLabel, spin, hideNext }})
            }
        });
    }

    void ShowGameDashboard()
    {
        foreach (var p in _players) p.UsedChallengeThisRound = false;
        _currentSpeakerIndex = 0;
        var day = Btn("Day: Speaker Turns"); day.Clicked += (_, _) => ShowSpeakerTurn();
        var night = Btn("Night Phase Guide", Color.FromArgb("#312E81")); night.Clicked += (_, _) => ShowNight();
        var vote = Btn("Voting / Kill Player", Color.FromArgb("#7F1D1D")); vote.Clicked += (_, _) => ShowVoting();
        var reset = Btn("Reset Game", Color.FromArgb("#E5E7EB"), Dark); reset.Clicked += (_, _) => ShowHome();
        Set(new VerticalStackLayout { Padding=16, Children={ H("Game Started"), CardBox(new VerticalStackLayout{Children={day, night, vote, reset}}) } });
    }

    IEnumerable<Player> AlivePlayers() => _players.Where(x => x.IsAlive);

    void ShowSpeakerTurn()
    {
        var alive = AlivePlayers().ToList();
        if (!alive.Any()) { ShowGameDashboard(); return; }
        if (_currentSpeakerIndex >= alive.Count)
        {
            foreach (var p in _players) p.UsedChallengeThisRound = false;
            _currentSpeakerIndex = 0;
            DisplayAlert("Round Complete", "All players spoke. Challenge limits reset for next round.", "OK");
        }
        var speaker = alive[_currentSpeakerIndex];
        var timerLabel = H($"{_mainSeconds}", 56);
        var info = new Label { Text = $"Speaker: {speaker.Name}", FontSize=24, FontAttributes=FontAttributes.Bold, TextColor=Dark, HorizontalTextAlignment=TextAlignment.Center };
        var start = Btn("Start Speak Timer");
        start.Clicked += async (_, _) => await RunTimer(timerLabel, _mainSeconds, "Time finished");
        var challenge = Btn("Challenge Before/After", Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"));
        challenge.Clicked += (_, _) => ShowChallengePicker(speaker);
        var next = Btn("Next Speaker", Color.FromArgb("#111827"));
        next.Clicked += (_, _) => { StopTimer(); _currentSpeakerIndex++; ShowSpeakerTurn(); };
        var back = Btn("Dashboard", Color.FromArgb("#E5E7EB"), Dark); back.Clicked += (_, _) => { StopTimer(); ShowGameDashboard(); };
        Set(new VerticalStackLayout { Padding=16, Children={ H("Speaker Turn"), CardBox(new VerticalStackLayout{Spacing=12, Children={info, timerLabel, start, challenge, next, back}}) } });
    }

    void ShowChallengePicker(Player speaker)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        foreach (var p in AlivePlayers().Where(x => x != speaker))
        {
            var b = Btn(p.UsedChallengeThisRound ? $"{p.Name} already used challenge" : $"{p.Name} challenge", p.UsedChallengeThisRound ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#FEF3C7"), p.UsedChallengeThisRound ? Muted : Color.FromArgb("#92400E"));
            b.IsEnabled = !p.UsedChallengeThisRound;
            b.Clicked += async (_, _) =>
            {
                p.UsedChallengeThisRound = true;
                await SpeakChallengeSound();
                ShowChallengeTimer(p.Name);
            };
            stack.Children.Add(b);
        }
        var back = Btn("Back", Color.FromArgb("#E5E7EB"), Dark); back.Clicked += (_, _) => ShowSpeakerTurn();
        stack.Children.Add(back);
        Set(new VerticalStackLayout { Padding=16, Children={ H("Select Challenger"), CardBox(stack) } });
    }

    void ShowChallengeTimer(string name)
    {
        var timerLabel = H($"{_challengeSeconds}", 56);
        var start = Btn("Start Challenge Timer", Color.FromArgb("#F59E0B"));
        start.Clicked += async (_, _) => { await RunTimer(timerLabel, _challengeSeconds, "Challenge finished"); ShowSpeakerTurn(); };
        Set(new VerticalStackLayout { Padding=16, Children={ H($"Challenge: {name}"), CardBox(new VerticalStackLayout{Spacing=12, Children={timerLabel, start}}) } });
    }

    void ShowVoting()
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        foreach (var p in _players)
        {
            var b = Btn(p.IsAlive ? $"Eliminate {p.Name}" : $"Revive {p.Name}", p.IsAlive ? Color.FromArgb("#FEE2E2") : Color.FromArgb("#DCFCE7"), p.IsAlive ? Color.FromArgb("#991B1B") : Color.FromArgb("#166534"));
            b.Clicked += (_, _) => { p.IsAlive = !p.IsAlive; ShowVoting(); };
            stack.Children.Add(b);
        }
        var back = Btn("Dashboard", Color.FromArgb("#E5E7EB"), Dark); back.Clicked += (_, _) => ShowGameDashboard();
        stack.Children.Add(back);
        Set(new VerticalStackLayout { Padding=16, Children={ H("Voting / Player Status"), CardBox(stack) } });
    }

    void ShowNight()
    {
        var alive = AlivePlayers().ToList();
        var text = string.Join("\n", new[]
        {
            "1. Everyone closes eyes.",
            "2. Mafia wake up and choose target.",
            "3. Doctor wakes and saves one player.",
            "4. Detective wakes and checks one player.",
            "5. Special roles act if your scenario has them.",
            "6. Start next day and announce result."
        });
        var list = string.Join("\n", alive.Select(x => $"{x.Number}. {x.Name}"));
        var back = Btn("Back to Dashboard"); back.Clicked += (_, _) => ShowGameDashboard();
        Set(new VerticalStackLayout { Padding=16, Children={ H("Night Phase"), CardBox(new VerticalStackLayout{Spacing=12, Children={ new Label{Text=text, FontSize=19, TextColor=Dark}, new Label{Text="Alive players:\n"+list, FontSize=17, TextColor=Muted}, back }}) } });
    }

    async Task RunTimer(Label label, int seconds, string alert)
    {
        StopTimer();
        _timerCts = new CancellationTokenSource();
        try
        {
            for (int s = seconds; s >= 0; s--)
            {
                label.Text = s.ToString();
                await Task.Delay(1000, _timerCts.Token);
            }
            await TryBeep();
            await DisplayAlert("Timer", alert, "OK");
        }
        catch (TaskCanceledException) { }
    }

    void StopTimer()
    {
        _timerCts?.Cancel();
        _timerCts = null;
    }

    async Task TryBeep()
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }
        await Task.CompletedTask;
    }

    async Task SpeakChallengeSound()
    {
        // Uses device vibration as built-in accessible cue. You can add real audio files later in Resources/Raw.
        await TryBeep();
    }
}
