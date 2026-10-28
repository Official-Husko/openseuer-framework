using ObenFramework.Economy;

namespace ObenCore.Tests.Economy;

public class LedgerBookTests
{
    private readonly LedgerSave _save = new();
    private readonly List<LedgerEntry> _added = new();

    private static AccountValues Account(int coins, float rm = 0f, int debtCoins = 0, float debtRm = 0f, bool frozen = false, string bank = "Stalburg Bank") =>
        new(bank, coins, rm, debtCoins, debtRm, frozen);

    private void Baseline(AccountValues values)
    {
        LedgerBook.Track(_save, values, LedgerKind.Other, 1, null);
        _save.Baselined = true;
    }

    [Fact]
    public void FirstSightIsAnOpeningBalance()
    {
        Assert.Equal(1, LedgerBook.Track(_save, Account(100, 2.5f), LedgerKind.Other, 3, _added));
        var entry = Assert.Single(_added);
        Assert.Equal(LedgerKind.Opening, entry.Kind);
        Assert.Equal(100, entry.Coins);
        Assert.Equal(2.5f, entry.RealMoney);
        Assert.Equal(1, entry.Id);
        Assert.Equal(3, _save.FirstDay);
    }

    [Fact]
    public void AccountsAppearingLaterAreOpened()
    {
        _save.Baselined = true;
        LedgerBook.Track(_save, Account(0), LedgerKind.Other, 1, _added);
        Assert.Equal(LedgerKind.Opened, _added[0].Kind);
    }

    [Fact]
    public void NothingChangedWritesNothing()
    {
        Baseline(Account(100));
        Assert.Equal(0, LedgerBook.Track(_save, Account(100), LedgerKind.Deposit, 2, _added));
        Assert.Empty(_added);
    }

    [Fact]
    public void FloatNoiseIsNotAChange()
    {
        Baseline(Account(0, 1.3f));
        Assert.Equal(0, LedgerBook.Track(_save, Account(0, 1.30001f), LedgerKind.Other, 2, _added));
    }

    [Fact]
    public void DepositAndWithdrawalAreToldApartBySign()
    {
        Baseline(Account(100));
        LedgerBook.Track(_save, Account(150), LedgerKind.Deposit, 2, _added);
        LedgerBook.Track(_save, Account(120), LedgerKind.Deposit, 2, _added);
        Assert.Equal(LedgerKind.Deposit, _added[0].Kind);
        Assert.Equal(50, _added[0].Coins);
        Assert.Equal(LedgerKind.Withdrawal, _added[1].Kind);
        Assert.Equal(-30, _added[1].Coins);
        Assert.Equal(120, _added[1].CoinsAfter);
    }

    [Fact]
    public void RealMoneyDecidesTheSignWhenCoinsDidNotMove()
    {
        Baseline(Account(0, 10f));
        LedgerBook.Track(_save, Account(0, 4f), LedgerKind.Withdrawal, 2, _added);
        Assert.Equal(LedgerKind.Withdrawal, _added[0].Kind);
        Assert.Equal(-6f, _added[0].RealMoney);
    }

    [Fact]
    public void DebtChangesAreRecorded()
    {
        Baseline(Account(0));
        LedgerBook.Track(_save, Account(0, 0f, 0, 1000f), LedgerKind.Loan, 2, _added);
        Assert.Equal(LedgerKind.Loan, _added[0].Kind);
        Assert.Equal(1000f, _added[0].DebtRealMoney);
    }

    [Fact]
    public void AFreezeAloneIsItsOwnKind()
    {
        Baseline(Account(10));
        LedgerBook.Track(_save, Account(10, frozen: true), LedgerKind.Other, 2, _added);
        LedgerBook.Track(_save, Account(10), LedgerKind.Other, 2, _added);
        Assert.Equal(LedgerKind.Frozen, _added[0].Kind);
        Assert.Equal(LedgerKind.Unfrozen, _added[1].Kind);
    }

    [Fact]
    public void MoneyAndAFreezeTogetherGiveTwoEntries()
    {
        Baseline(Account(10));
        Assert.Equal(2, LedgerBook.Track(_save, Account(30, frozen: true), LedgerKind.ScamPayout, 2, _added));
        Assert.Equal(LedgerKind.ScamPayout, _added[0].Kind);
        Assert.Equal(LedgerKind.Frozen, _added[1].Kind);
        Assert.Equal(_added[0].Id + 1, _added[1].Id);
    }

    [Fact]
    public void EntriesAreNewestFirstPerBankAndPage()
    {
        Baseline(Account(0));
        Baseline(Account(0, bank: "Other Bank"));
        for (int i = 1; i <= 5; i++)
        {
            LedgerBook.Track(_save, Account(i), LedgerKind.Deposit, i, null);
            LedgerBook.Track(_save, Account(i, bank: "Other Bank"), LedgerKind.Deposit, i, null);
        }

        var first = LedgerBook.Entries(_save, "Stalburg Bank", 2, 0);
        Assert.Equal(2, first.Count);
        Assert.Equal(5, first[0].CoinsAfter);
        Assert.Equal(4, first[1].CoinsAfter);
        Assert.All(first, e => Assert.Equal("Stalburg Bank", e.Bank));

        var next = LedgerBook.Entries(_save, "Stalburg Bank", 10, first[1].Id);
        Assert.Equal(4, next.Count); // 3, 2, 1 and the opening balance
        Assert.Equal(LedgerKind.Opening, next[^1].Kind);
    }

    [Fact]
    public void EntriesAreCopies()
    {
        Baseline(Account(5));
        var entry = LedgerBook.Entries(_save, "Stalburg Bank", 1, 0)[0];
        entry.Coins = 999;
        Assert.Equal(5, _save.Entries[0].Coins);
    }

    [Fact]
    public void OldestEntriesOfABankAreTrimmed()
    {
        Baseline(Account(0));
        for (int i = 1; i <= LedgerBook.CapPerBank + 10; i++)
            LedgerBook.Track(_save, Account(i), LedgerKind.Deposit, i, null);
        Assert.Equal(LedgerBook.CapPerBank, _save.Entries.Count);
        Assert.Equal(11, _save.Entries[0].CoinsAfter);
    }

    [Fact]
    public void AForgottenAccountStartsOverAsOpened()
    {
        Baseline(Account(50));
        Assert.True(LedgerBook.Forget(_save, "Stalburg Bank"));
        LedgerBook.Track(_save, Account(0), LedgerKind.Other, 2, _added);
        Assert.Equal(LedgerKind.Opened, _added[0].Kind);
    }
}
