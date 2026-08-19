using UnityEngine;

namespace Tycoon
{
    /// <summary>
    /// Rare, narrated world events - four market shocks (Plague, Inflation, War,
    /// Storm) and four lucky windfalls (Birthday Gift, Inheritance, Lottery Win,
    /// Investor Bonus) - layered on top of MarketManager's ambient month-to-month
    /// drift. This is the game's signature system: instead of every property just
    /// wandering independently, an announced event drives prices (or hands you
    /// cash outright) with real narrative logic - a storm wrecks flimsy tents but
    /// leaves solid houses relatively more valuable; a plague tanks demand for
    /// real estate across the board; a lottery win just lands in your pocket.
    /// Ticked once per in-game month from CalendarManager.Tick(), same pattern as
    /// every other manager.
    /// </summary>
    public class WorldEventManager : MonoBehaviour
    {
        public enum WorldEventType { Plague, Inflation, War, Storm, BirthdayGift, Inheritance, LotteryWin, InvestorBonus }

        const float TriggerChancePerMonth = 0.14f;
        const int EventDurationMonths = 3;
        const int CooldownAfterEventMonths = 2;
        const float AnnouncementDuration = 3.6f; // roughly matches the full-screen banner's total on-screen time

        GameManager game;

        WorldEventType? activeEvent;
        int monthsRemaining;
        int cooldownMonths;

        public void Init(GameManager owner)
        {
            game = owner;
        }

        public bool HasActiveEvent => activeEvent.HasValue;

        /// <summary>For SaveManager only. A pending-but-unconfirmed event (the
        /// modal is up, player hasn't tapped the button yet) intentionally isn't
        /// captured here - it only lives in a UI callback closure, not in this
        /// manager's own state, so it's simply not there again after a reload.
        /// Harmless: no cash/effect was ever applied for it, and new events keep
        /// rolling normally afterward.</summary>
        public (bool hasEvent, WorldEventType type, int monthsRemaining, int cooldownMonths) GetSaveState()
            => (activeEvent.HasValue, activeEvent ?? default, monthsRemaining, cooldownMonths);

        public void RestoreState(bool hasEvent, WorldEventType type, int savedMonthsRemaining, int savedCooldownMonths)
        {
            activeEvent = hasEvent ? type : (WorldEventType?)null;
            monthsRemaining = savedMonthsRemaining;
            cooldownMonths = savedCooldownMonths;
        }

        public void Tick()
        {
            // A confirmation modal (new event, or the "All Clear" follow-up) is
            // still on screen - nothing about the calendar pauses for it, so
            // without this guard a slow-to-click player (especially at 2x/3x
            // speed, where a month can pass in under 2 seconds) could have a
            // second event roll and silently overwrite the first one's modal -
            // same text fields, same button listener - before they ever saw it.
            // Also deferred while Settings is open - a "WAR!" banner popping up
            // full-screen over the settings menu the player is actively
            // browsing would be jarring, and Settings has no queue of its own
            // to defer behind.
            if (game.Hud.EventConfirmationOpen || game.Settings.IsOpen) return;

            if (activeEvent.HasValue)
            {
                ApplyEventBias(activeEvent.Value);
                monthsRemaining--;
                if (monthsRemaining <= 0)
                {
                    // Calmer follow-up: no siren flash, no red strobe - the danger
                    // already passed, this is just the all-clear. Nothing left to
                    // apply, so onConfirm is a no-op; the button's only job here is
                    // letting the player dismiss it.
                    game.Hud.QueueEventConfirmation("All Clear", EndText(activeEvent.Value), Color.white, alarm: false, buttonLabel: "OK", onConfirm: null);
                    activeEvent = null;
                    cooldownMonths = CooldownAfterEventMonths;
                }
                return;
            }

            if (cooldownMonths > 0)
            {
                cooldownMonths--;
                return;
            }

            if (Random.value < TriggerChancePerMonth)
                TriggerEvent((WorldEventType)Random.Range(0, 8));
        }

        /// <summary>Rolling an event only opens the confirmation modal - nothing
        /// actually happens (no market shift, no cash, no sirens) until the player
        /// taps through it. That tap is also the natural hook point for a future
        /// "watch an ad to skip the bad one / double the good one" button.</summary>
        void TriggerEvent(WorldEventType type)
        {
            if (IsPositive(type))
            {
                int grant = ComputeGrant(type);
                game.Hud.QueueEventConfirmation(
                    EventName(type).ToUpperInvariant() + "!",
                    $"{PositiveText(type)} Claim your {EconomyManager.FormatSigned(grant)}!",
                    EventColor(type), alarm: false,
                    buttonLabel: $"Claim {EconomyManager.FormatMoney(grant)}",
                    onConfirm: () => ApplyPositiveEvent(type, grant));
            }
            else
            {
                game.Hud.QueueEventConfirmation(
                    EventName(type).ToUpperInvariant() + "!",
                    StartText(type),
                    EventColor(type), alarm: true,
                    buttonLabel: "OK",
                    onConfirm: () => ApplyNegativeEvent(type));
            }
        }

        /// <summary>Instant, one-time cash - no ongoing market effect, no siren.
        /// Still shares the cooldown with the bad events so *something* notable
        /// doesn't happen every single month.</summary>
        void ApplyPositiveEvent(WorldEventType type, int grant)
        {
            game.Economy.balance += grant;
            game.Economy.sessionProfit += grant;
            game.World.SpawnCashEventEffect(EconomyManager.FormatSigned(grant), EventColor(type));
            game.Hud.Refresh();
            cooldownMonths = CooldownAfterEventMonths;
        }

        /// <summary>The event "starts" here, not when it was rolled - sirens flash
        /// now, and the first market bias applies on the next monthly tick.</summary>
        void ApplyNegativeEvent(WorldEventType type)
        {
            activeEvent = type;
            monthsRemaining = EventDurationMonths;
            game.World.FlashSirens(AnnouncementDuration);
            game.Hud.Refresh();
        }

        static bool IsPositive(WorldEventType type) =>
            type is WorldEventType.BirthdayGift or WorldEventType.Inheritance or WorldEventType.LotteryWin or WorldEventType.InvestorBonus;

        /// <summary>Percentage-of-balance (or, for InvestorBonus, percentage-of-net-worth)
        /// instead of a flat dollar amount, so a "windfall" stays meaningful whether
        /// it lands at $100 or $50,000 - with a small floor so it's never trivial
        /// right at the start.</summary>
        int ComputeGrant(WorldEventType type)
        {
            int balance = game.Economy.balance;
            int netWorth = game.Economy.ComputeNetWorth();
            return type switch
            {
                WorldEventType.BirthdayGift => Mathf.Max(15, Mathf.RoundToInt(balance * 0.12f)),
                WorldEventType.Inheritance => Mathf.Max(40, Mathf.RoundToInt(balance * 0.30f)),
                WorldEventType.LotteryWin => Mathf.Max(25, Mathf.RoundToInt(balance * Random.Range(0.15f, 0.4f))),
                WorldEventType.InvestorBonus => Mathf.Max(25, Mathf.RoundToInt(netWorth * 0.12f)),
                _ => 0,
            };
        }

        /// <summary>Same exclusion rule as MarketManager.FluctuateMarket - a plot
        /// mid-decision (lease just expired) is frozen so its options don't shift
        /// under the player while they're choosing.</summary>
        void ApplyEventBias(WorldEventType type)
        {
            var plots = game.Plots.plots;
            if (plots == null) return;
            foreach (var state in plots)
            {
                if (state.ownership == PropertyOwnership.NeedsDecision) continue;

                float bias = EventBiasPercent(type, state.definition.tier);
                float delta = state.marketValue * bias;
                state.marketValue = Mathf.Max(1f, state.marketValue + delta);
                state.lastDeltaPositive = delta >= 0;
                game.Plots.RefreshPriceLabel(state);
            }
        }

        /// <summary>War and Storm share the same logic: flimsy tents lose value
        /// (damaged/undesirable), every sturdier tier gains (safe-haven demand for
        /// solid shelter) - exactly the asymmetric effect that makes these events
        /// interesting instead of just another uniform market wobble.</summary>
        static float EventBiasPercent(WorldEventType evt, PropertyTier tier) => evt switch
        {
            WorldEventType.Plague => -0.09f,
            WorldEventType.Inflation => 0.08f,
            WorldEventType.War or WorldEventType.Storm => tier == PropertyTier.Tent ? -0.12f : 0.06f,
            _ => 0f,
        };

        static string StartText(WorldEventType type) => type switch
        {
            WorldEventType.Plague => "Plague outbreak! Real estate demand is collapsing.",
            WorldEventType.Inflation => "Inflation is spiking! Prices are surging across the board.",
            WorldEventType.War => "War has broken out! Solid houses are in demand - tents are losing value fast.",
            WorldEventType.Storm => "Storm approaching! Tents will take damage - solid houses hold firm.",
            _ => "",
        };

        static string PositiveText(WorldEventType type) => type switch
        {
            WorldEventType.BirthdayGift => "Happy birthday! A gift arrives from a loved one.",
            WorldEventType.Inheritance => "A distant relative left you an inheritance.",
            WorldEventType.LotteryWin => "You won the lottery!",
            WorldEventType.InvestorBonus => "An investor is impressed by your growing portfolio.",
            _ => "",
        };

        static string EndText(WorldEventType type) => type switch
        {
            WorldEventType.Plague => "The plague has passed. The market stabilizes.",
            WorldEventType.Inflation => "Inflation cools off. Prices settle back down.",
            WorldEventType.War => "The war has ended. The market returns to normal.",
            WorldEventType.Storm => "The storm has passed. The market calms down.",
            _ => "",
        };

        static Color EventColor(WorldEventType type) => type switch
        {
            WorldEventType.Plague => new Color(0.45f, 0.62f, 0.35f),
            WorldEventType.Inflation => new Color(0.95f, 0.65f, 0.2f),
            WorldEventType.War => new Color(0.85f, 0.25f, 0.22f),
            WorldEventType.Storm => new Color(0.4f, 0.5f, 0.62f),
            WorldEventType.BirthdayGift => new Color(0.95f, 0.55f, 0.75f),
            WorldEventType.Inheritance => new Color(0.55f, 0.45f, 0.75f),
            WorldEventType.LotteryWin => new Color(0.95f, 0.8f, 0.2f),
            WorldEventType.InvestorBonus => new Color(0.3f, 0.75f, 0.55f),
            _ => Color.white,
        };

        public string ActiveStatusText()
        {
            if (!activeEvent.HasValue) return null;
            return $"{EventName(activeEvent.Value)} - {monthsRemaining}mo left";
        }

        public Color ActiveEventColor() => activeEvent.HasValue ? EventColor(activeEvent.Value) : Color.white;

        static string EventName(WorldEventType type) => type switch
        {
            WorldEventType.Plague => "Plague",
            WorldEventType.Inflation => "Inflation",
            WorldEventType.War => "War",
            WorldEventType.Storm => "Storm",
            WorldEventType.BirthdayGift => "Birthday Gift",
            WorldEventType.Inheritance => "Inheritance",
            WorldEventType.LotteryWin => "Lottery Win",
            WorldEventType.InvestorBonus => "Investor Bonus",
            _ => "",
        };
    }
}
