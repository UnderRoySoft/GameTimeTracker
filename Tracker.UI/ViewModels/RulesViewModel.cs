using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tracker.Data.Repositories;

namespace Tracker.UI.ViewModels
{
    public partial class RulesViewModel : ObservableObject
    {
        private readonly RuleRepository _rules;
        private readonly GameRepository _games;

        public ObservableCollection<RuleRowViewModel> Items { get; } = new();

        [ObservableProperty] private string gameName = "";
        [ObservableProperty] private string exeName = "";
        [ObservableProperty] private int priority = 100;

        public RulesViewModel(RuleRepository rules, GameRepository games)
        {
            _rules = rules;
            _games = games;
        }

        public async Task LoadAsync()
        {
            Items.Clear();

            var rows = await _rules.ListRulesWithGameAsync();

            foreach (var r in rows)
            {
                Items.Add(new RuleRowViewModel
                {
                    RuleId = r.RuleId,
                    GameName = r.GameName,
                    ExecutableName = r.ExecutableName,
                    Priority = r.Priority
                });
            }
        }

        [RelayCommand]
        private async Task AddAsync()
        {
            try
            {
                var g = (GameName ?? "").Trim();
                var e = (ExeName ?? "").Trim();

                if (string.IsNullOrWhiteSpace(g))
                {
                    MessageBox.Show("Game name is empty.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(e))
                {
                    MessageBox.Show("Exe name is empty. Use e.g. witcher.exe (NOT full path).");
                    return;
                }

                if (e.Contains("\\") || e.Contains("/"))
                    e = System.IO.Path.GetFileName(e);

                e = e.Trim().ToLowerInvariant();

                var gameId = await _games.CreateOrGetAsync(g);
                await _rules.AddRuleAsync(gameId, e, Priority);

                await LoadAsync();

                GameName = "";
                ExeName = "";
                Priority = 100;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Add rule failed");
            }
        }

        // ✅ NEW: Delete command (generated name: DeleteCommand)
        [RelayCommand]
        private async Task DeleteAsync(long ruleId)
        {
            try
            {
                if (ruleId <= 0) return;

                var confirm = MessageBox.Show(
                    $"Delete rule #{ruleId}?",
                    "Confirm delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                    return;

                await _rules.DeleteRuleAsync(ruleId);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Delete rule failed");
            }
        }
    }

    public sealed class RuleRowViewModel : ObservableObject
    {
        public long RuleId { get; set; }
        public string GameName { get; set; } = "";
        public string ExecutableName { get; set; } = "";
        public int Priority { get; set; }
    }
}