using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TooFishy
{
    public class Inventory
    {
        public List<InventoryItem> Items { get; private set; } = new();
        public int FishesCaught { get; private set; }
        public float TotalWeight { get; private set; }
        public int TotalValue { get; private set; }

        GameState _state;

        public void Bind(GameState state) => _state = state;

        public int GetMaxWeight()
        {
            int cargoLevel = _state.GetUpgradeLevel(Upgrade.CargoSize) + 1;
            if (cargoLevel <= 4)
                return 25 * cargoLevel;
            return 100 + 50 * (cargoLevel - 4);
        }

        public bool Add(InventoryItem item)
        {
            if (Items.Any(i => i.Id == item.Id))
                return false;

            if (item.Weight + TotalWeight > GetMaxWeight())
            {
                if (_state.GetUpgradeLevel(Upgrade.InventoryManagement) > 0)
                    return TryReplaceLessValuable(item);
                return false;
            }

            Items.Add(item);
            UpdateTotals();
            _state?.NotifyInventoryUpdated();
            return true;
        }

        bool TryReplaceLessValuable(InventoryItem newFish)
        {
            if (Items.Count == 0) return false;

            var sorted = Items.OrderBy(i => i.Price).ToList();
            foreach (var item in sorted)
            {
                if (item.Price >= newFish.Price) continue;
                if (item.Weight >= newFish.Weight)
                {
                    Items.Remove(item);
                    Items.Add(newFish);
                    UpdateTotals();
                    _state?.NotifyInventoryUpdated();
                    return true;
                }
            }

            float spaceNeeded = newFish.Weight - (GetMaxWeight() - TotalWeight);
            if (spaceNeeded <= 0f)
            {
                Items.Add(newFish);
                UpdateTotals();
                _state?.NotifyInventoryUpdated();
                return true;
            }

            var byRatio = Items.OrderBy(i => i.Price / Mathf.Max(0.01f, i.Weight)).ToList();
            var toRemove = new List<InventoryItem>();
            float weightRemoved = 0f;
            int valueRemoved = 0;

            foreach (var item in byRatio)
            {
                if (item.Price > newFish.Price) continue;
                toRemove.Add(item);
                weightRemoved += item.Weight;
                valueRemoved += item.Price;
                if (weightRemoved >= spaceNeeded) break;
            }

            if (weightRemoved < spaceNeeded || valueRemoved >= newFish.Price)
                return false;

            foreach (var item in toRemove)
                Items.Remove(item);

            Items.Add(newFish);
            UpdateTotals();
            _state?.NotifyInventoryUpdated();
            return true;
        }

        public int SellItems()
        {
            int sold = TotalValue;
            Items.Clear();
            UpdateTotals();
            if (_state != null)
            {
                _state.Money += sold;
                _state.NotifyInventoryUpdated();
            }
            return sold;
        }

        public void Clear()
        {
            Items.Clear();
            UpdateTotals();
            _state?.NotifyInventoryUpdated();
        }

        void UpdateTotals()
        {
            int value = 0;
            float weight = 0f;
            foreach (var item in Items)
            {
                value += item.Price;
                weight += item.Weight;
            }
            TotalValue = value;
            TotalWeight = weight;
            FishesCaught = Items.Count;
        }
    }
}
