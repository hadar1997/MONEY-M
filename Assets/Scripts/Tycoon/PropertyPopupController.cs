using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Tycoon
{
    /// <summary>
    /// The Buy/Sell/Lease decision popup and the actions it triggers. Ownership
    /// only ever begins with a Buy at market price; leasing a property out to a
    /// tenant (and, 12 months later, renewing that lease or selling) is something
    /// you do *with* a property you already own, never a separate cheaper way to
    /// acquire one. Reads/writes EconomyManager balance and PlotManager plot
    /// state; owns no state of its own beyond the popup's UI widgets and which
    /// plot is open.
    /// </summary>
    public class PropertyPopupController : MonoBehaviour
    {
        GameManager game;

        public GameObject popupPanel;
        Text popupTitle;
        Button buyButton;
        Text buyButtonLabel;
        Button secondaryButton;
        Text secondaryButtonLabel;
        Button cancelButton;
        Text cancelButtonLabel;

        int activeIndex = -1;
        RectTransform cardTransform;

        public void Init(GameManager owner)
        {
            game = owner;
        }

        public void Build()
        {
            var root = game.Hud.CanvasRoot;
            var font = game.Font;

            popupPanel = UIFactory.CreateFullscreenImage(root, "PopupPanel", new Color(0, 0, 0, 0.65f)).gameObject;

            var card = UIFactory.CreateImage(popupPanel.transform, "Card", new Color(0.12f, 0.13f, 0.18f, 0.98f), Vector2.zero, new Vector2(760, 480));
            cardTransform = card.rectTransform;
            popupTitle = UIFactory.CreateText(card.transform, "Title", "", 30, Color.white, new Vector2(0, 170), new Vector2(680, 70), font);

            buyButton = UIFactory.CreateButton(card.transform, "BuyButton", new Color(0.2f, 0.7f, 0.3f), new Vector2(0, 40), new Vector2(600, 80), font, out buyButtonLabel);
            secondaryButton = UIFactory.CreateButton(card.transform, "SecondaryButton", new Color(0.25f, 0.5f, 0.85f), new Vector2(0, -60), new Vector2(600, 80), font, out secondaryButtonLabel);
            cancelButton = UIFactory.CreateButton(card.transform, "CancelButton", new Color(0.6f, 0.25f, 0.25f), new Vector2(0, -160), new Vector2(600, 80), font, out cancelButtonLabel);

            popupPanel.SetActive(false);
        }

        public bool IsOpen => popupPanel.activeSelf;

        /// <summary>For PlotManager.UpdatePlotExpiry only - -1 while closed.</summary>
        public int ActiveIndex => IsOpen ? activeIndex : -1;

        /// <summary>Re-renders the open popup against current data every frame
        /// (see GameManager.Update). The market keeps ticking while a decision
        /// is pending, so without this the price you *see* and the price
        /// ConfirmBuy/SignLease actually charges (always the live marketValue)
        /// could drift apart the moment a tick lands while you're deciding.</summary>
        public void RefreshIfOpen()
        {
            if (IsOpen && activeIndex >= 0) Open(activeIndex);
        }

        public void Open(int index)
        {
            bool justOpened = !IsOpen || activeIndex != index;
            activeIndex = index;
            var state = game.Plots.plots[index];
            var def = state.definition;

            popupPanel.SetActive(true);
            if (justOpened) StartCoroutine(PlayOpenAnimation());

            // Every branch below assumes both action buttons start visible;
            // only the Rented branch needs to hide both.
            buyButton.gameObject.SetActive(true);
            secondaryButton.gameObject.SetActive(true);

            if (state.ownership == PropertyOwnership.Rented)
            {
                popupTitle.text = $"{def.displayName} - leased, {state.leaseMonthsRemaining} mo left ({EconomyManager.FormatSigned(state.lockedRent)}/mo). Nothing to do until the lease ends.";

                buyButton.gameObject.SetActive(false);
                secondaryButton.gameObject.SetActive(false);

                cancelButtonLabel.text = "Close";
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(Close);
                return;
            }

            if (state.ownership == PropertyOwnership.Owned || state.ownership == PropertyOwnership.NeedsDecision)
            {
                // You already own this one (that's how you got here - Buy is the
                // only way in). From here you can sell it, lease it out to a
                // tenant for 12 months, or just close the popup and keep holding
                // it bare while you wait for a better market.
                bool needsDecision = state.ownership == PropertyOwnership.NeedsDecision;
                int sellPrice = Mathf.RoundToInt(state.marketValue);
                int diff = sellPrice - state.purchasePrice;
                string diffText = diff >= 0 ? $"profit {EconomyManager.FormatSigned(diff)}" : $"loss {EconomyManager.FormatMoney(diff)}";
                int monthlyRent = game.Market.ComputeMonthlyRent(state.marketValue);

                popupTitle.text = needsDecision
                    ? $"{def.displayName} - lease ended, worth {EconomyManager.FormatMoney(sellPrice)} ({diffText})"
                    : $"{def.displayName} - worth {EconomyManager.FormatMoney(sellPrice)} ({diffText})";

                buyButtonLabel.text = $"Sell for {EconomyManager.FormatMoney(sellPrice)}";
                buyButton.interactable = true;
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(() => ConfirmSell(index));

                secondaryButtonLabel.text = needsDecision
                    ? $"Renew lease - {EconomyManager.FormatMoney(monthlyRent)}/mo"
                    : $"Lease out - {EconomyManager.FormatMoney(monthlyRent)}/mo";
                secondaryButton.interactable = true;
                secondaryButton.onClick.RemoveAllListeners();
                secondaryButton.onClick.AddListener(() => SignLease(index));

                cancelButtonLabel.text = needsDecision ? "Keep holding" : "Cancel";
                cancelButton.onClick.RemoveAllListeners();
                if (needsDecision)
                    cancelButton.onClick.AddListener(() => KeepHolding(index));
                else
                    cancelButton.onClick.AddListener(Close);
                return;
            }

            // Unowned: the only way in is to buy it outright at market price.
            int buyPrice = Mathf.RoundToInt(state.marketValue);
            popupTitle.text = def.displayName;

            buyButtonLabel.text = $"Buy for {EconomyManager.FormatMoney(buyPrice)}";
            buyButton.interactable = game.Economy.balance >= buyPrice;
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => ConfirmBuy(index));

            secondaryButton.gameObject.SetActive(false);

            cancelButtonLabel.text = "Cancel";
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(Close);
        }

        public void Close()
        {
            popupPanel.SetActive(false);
            activeIndex = -1;
        }

        /// <summary>Quick scale-in punch so the popup feels like it responded to
        /// the tap instead of just snapping into existence.</summary>
        IEnumerator PlayOpenAnimation()
        {
            const float duration = 0.12f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime; // unaffected by pause/speed controls
                float p = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - p) * (1f - p);
                cardTransform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, eased);
                yield return null;
            }
            cardTransform.localScale = Vector3.one;
        }

        void ConfirmBuy(int index)
        {
            var state = game.Plots.plots[index];
            int price = Mathf.RoundToInt(state.marketValue);
            if (game.Economy.balance < price) return;

            game.Economy.balance -= price;
            state.purchasePrice = price;
            state.ownership = PropertyOwnership.Owned;
            // Deliberately no passive income here: owning a property is a pure
            // speculative asset (buy low, sell high via MarketManager.FluctuateMarket) -
            // matching the reference game's "buy cheap, sell expensive" loop.
            // Only rented properties generate income (MarketManager.PayMonthlyRent).
            state.view.Refresh();
            game.Plots.RefreshPriceLabel(state);
            game.Hud.Refresh();
            Close();
        }

        /// <summary>Leases an already-owned plot out to a tenant for a 12-month
        /// term at the current market rate (or renews an ended lease the same
        /// way). No cost to the owner - you already paid for the property when
        /// you bought it; from here on the rent is pure income, paid out monthly
        /// by MarketManager.PayMonthlyRent. Shared by the manual Lease/Renew
        /// button and MarketManager's auto-manager renewal so both paths stay in
        /// exact sync - only one place decides what a lease pays and how long it
        /// lasts.</summary>
        public void SignLease(int index)
        {
            var state = game.Plots.plots[index];
            var def = state.definition;
            int monthlyRent = game.Market.ComputeMonthlyRent(state.marketValue);

            state.ownership = PropertyOwnership.Rented;
            // Locked in for the whole lease term - PayMonthlyRent pays out this
            // fixed amount every month regardless of marketValue drift, so market
            // swings during the lease don't change what the tenant owes. Only
            // renewing (or signing a fresh lease after expiry) recomputes it from
            // the then-current marketValue.
            state.lockedRent = monthlyRent;
            state.leaseMonthsRemaining = def.leaseDurationMonths;

            state.view.Refresh();
            game.Plots.RefreshPriceLabel(state);
            game.Hud.Refresh();
            if (IsOpen && activeIndex == index) Close();
        }

        /// <summary>Ends the post-lease decision without renewing or selling -
        /// property reverts to plain ownership, still yours, just without a
        /// tenant until you choose to lease it out again.</summary>
        void KeepHolding(int index)
        {
            var state = game.Plots.plots[index];
            state.ownership = PropertyOwnership.Owned;
            state.view.Refresh();
            game.Plots.RefreshPriceLabel(state);
            Close();
        }

        void ConfirmSell(int index)
        {
            var state = game.Plots.plots[index];
            int sellPrice = Mathf.RoundToInt(state.marketValue);

            game.Economy.balance += sellPrice;
            game.Economy.sessionProfit += sellPrice - state.purchasePrice;
            // Sale proceeds land in your pocket the moment the popup closes, so
            // this is exactly the kind of cash-in-pocket moment coinPop is for -
            // fired before RerollPlot rebuilds the mesh underneath it (the
            // indicator itself is parented to the stable Map root, so it survives).
            game.World.SpawnFloatingIndicator(state, EconomyManager.FormatSigned(sellPrice), new Color(0.3f, 0.9f, 0.4f), coinPop: true);
            state.ownership = PropertyOwnership.Unowned;
            game.Plots.RerollPlot(state);
            game.Hud.Refresh();
            Close();
        }
    }
}
