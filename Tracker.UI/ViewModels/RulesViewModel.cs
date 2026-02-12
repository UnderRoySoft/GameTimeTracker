using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tracker.Data.Repositories;

namespace Tracker.UI.ViewModels
{
    public sealed partial class RulesViewModel : ObservableObject
    {
        private readonly GameRepository _games;
        private readonly RuleRepository _rules;

        public ObservableCollection<string> RulesList { get; } = new();

        [ObservableProperty] private string _gameName = "";
        [ObservableProperty] private string _exeName = "";
        [ObservableProperty] private int _priority = 1000;
        [ObservableProperty] private string _status = "Ready";

        public RulesViewModel(GameRepository games, RuleRepository rules)
        {
            _games = games;
            _rules = rules;
        }

        public async Task LoadAsync()
        {
            Status = "Loading…";
            RulesList.Clear();

            var rows = await _rules.ListRulesWithGameAsync();
            foreach (var r in rows)
                RulesList.Add($"{r.GameName}  ←  {r.ExecutableName}   (prio {r.Priority})");

            Status = $"Loaded {RulesList.Count} rule(s).";
        }

        [RelayCommand]
        private async Task AddRuleAsync()
        {
            var g = GameName?.Trim();
            var e = ExeName?.Trim();

            if (string.IsNullOrWhiteSpace(g) || string.IsNullOrWhiteSpace(e))
            {
                Status = "Game name and EXE name are required (e.g. witcher.exe).";
                return;
            }

            // Force exe name only (no full path)
            if (e.Contains("\\") || e.Contains("/"))
            {
                Status = "Use EXE name only (e.g. witcher.exe), not a full path.";
                return;
            }

            Status = "Saving…";
            var gameId = await _games.CreateOrGetAsync(g);
            await _rules.AddRuleAsync(gameId, e, Priority);

            GameName = "";
            ExeName = "";

            await LoadAsync();
            Status = "Rule added.";
        }
    }
}
