using System;
using System.Collections.Generic;
using System.Linq;

namespace SpiderGame.Game;

public class SpiderGame
{
    public List<Pile> Piles { get; } = new();
    public Stack<Card> Stock { get; private set; } = new();

    public int CompletedRuns { get; private set; }
    public int SuitCount => _suitCount;

    // n.º de "chamadas" ao stock que ainda faltam (cada uma = 10 cartas)
    public int RemainingDeals => Stock.Count / 10;

    private readonly Random _random = new();
    private int _suitCount = 1;

    public SpiderGame(int suitCount = 1)
    {
        _suitCount = NormalizeSuitCount(suitCount);
        NewGame(_suitCount);
    }

    #region  ...[New Game]...
    public void NewGame(int? suitCount = null)
    {
        _suitCount = NormalizeSuitCount(suitCount ?? _suitCount);

        Piles.Clear();
        for (int i = 0; i < 10; i++)
            Piles.Add(new Pile());

        CompletedRuns = 0;

        var deck = CreateDeck(_suitCount);
        Shuffle(deck);
        DealInitial(deck);
        Stock = new Stack<Card>(deck); // restante fica no Stock
    }
    #endregion

    #region ...[Create Deck]...
    private List<Card> CreateDeck(int suitCount)
    {
        suitCount = NormalizeSuitCount(suitCount);

        var cards = new List<Card>();
        var suits = Enum.GetValues<Suit>().Take(suitCount).ToList();

        // 2 baralhos = 104 cartas; ajustar cópias por naipe para manter o total
        int copiesPerSuit = 4 / suitCount; // suitCount permitido: 1,2,4

        for (int d = 0; d < 2; d++)
        {
            foreach (var suit in suits)
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    for (int i = 0; i < copiesPerSuit; i++)
                        cards.Add(new Card(suit, rank));
                }
            }
        }

        return cards;
    }
    #endregion

    #region ...[Shuffle]...
    private void Shuffle(List<Card> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }
    #endregion

    #region ...[Deal Initial]...
    private void DealInitial(List<Card> deck)
    {
        // Spider: 10 colunas, algumas com 6 cartas, outras com 5, última virada para cima
        for (int col = 0; col < 10; col++)
        {
            int cardsInPile = col < 4 ? 6 : 5; // 4 colunas com 6, 6 colunas com 5

            for (int c = 0; c < cardsInPile; c++)
            {
                var card = deck[0];
                deck.RemoveAt(0);

                // última carta da coluna fica virada para cima
                card.IsFaceUp = (c == cardsInPile - 1);
                Piles[col].AddCard(card);
            }
        }
    }
    #endregion

    #region ...[Can Deal From Stock]...
    public bool CanDealFromStock()
    {
        // regra: só precisa de 10 cartas no stock
        return Stock.Count >= 10;
    }
    #endregion

    #region ...[Deal From Stock]...
    public void DealFromStock()
    {
        if (!CanDealFromStock()) return;

        for (int i = 0; i < 10; i++)
        {
            var card = Stock.Pop();
            card.IsFaceUp = true;
            Piles[i].AddCard(card);
        }

        CheckAllPilesForCompletedRuns();
    }
    #endregion

    #region ...[Try Move Sequence]...
    // Movimento de uma sequência de cartas
    public bool TryMoveSequence(int fromPile, int fromIndex, int toPile)
    {
        if (fromPile == toPile) return false;
        if (fromPile < 0 || fromPile >= Piles.Count) return false;
        if (toPile < 0 || toPile >= Piles.Count) return false;

        var from = Piles[fromPile];
        var to = Piles[toPile];

        if (fromIndex < 0 || fromIndex >= from.Count)
            return false;

        var firstCard = from.Cards[fromIndex];
        if (!firstCard.IsFaceUp)
            return false;

        // até onde vai a sequência válida (mesmo naipe, descendente, face-up)
        int endIndex = GetMovableSequenceEnd(fromPile, fromIndex);
        int lastIndex = from.Count - 1;

        // só podes mover:
        // - a última carta da coluna (fromIndex == lastIndex)
        // - OU um bloco sequencial que termina na última carta (endIndex == lastIndex)
        if (endIndex != lastIndex && fromIndex != lastIndex)
            return false;

        int count = endIndex - fromIndex + 1;

        // validar destino
        if (to.Count > 0)
        {
            var destTop = to.Cards[to.Count - 1];

            if (!destTop.IsFaceUp)
                return false;

            if ((int)destTop.Rank != (int)firstCard.Rank + 1)
                return false;
        }

        // mover o bloco [fromIndex..endIndex]
        var moving = from.TakeRange(fromIndex, count);
        to.AddCards(moving);

        // virar nova carta do topo da origem, se existir
        if (from.TopCard is Card newTop && !newTop.IsFaceUp)
        {
            newTop.IsFaceUp = true;
        }

        CheckCompletedRuns(toPile);

        return true;
    }
    #endregion

    #region ...[Is Valid Descending Sequence]...
    private bool IsValidDescendingSequence(IReadOnlyList<Card> sequence)
    {
        if (sequence.Count == 0) return false;
        if (sequence.Count == 1) return true;

        for (int i = 0; i < sequence.Count - 1; i++)
        {
            var current = sequence[i];
            var next = sequence[i + 1];

            // mesmo naipe
            if (current.Suit != next.Suit) return false;

            // valor seguinte tem de ser 1 abaixo (ex: King -> Queen -> Jack ... )
            if ((int)current.Rank != (int)next.Rank + 1) return false;
        }

        return true;
    }
    #endregion

    #region ...[Can Place On Pile]...
    private bool CanPlaceOnPile(Card movingCard, Pile pile)
    {
        if (pile.TopCard is null) return true; // coluna vazia aceita qualquer carta

        var top = pile.TopCard;

        // regra: movingCard tem de ser 1 abaixo da carta do topo
        return (int)movingCard.Rank == (int)top!.Rank - 1;
    }
    #endregion

    #region ...[Check Completed Runs]...
    private void CheckAllPilesForCompletedRuns()
    {
        for (int i = 0; i < Piles.Count; i++)
            CheckCompletedRuns(i);
    }

    private void CheckCompletedRuns(int pileIndex)
    {
        var pile = Piles[pileIndex];

        bool removed;
        do
        {
            removed = false;

            if (pile.Count < 13)
                break;

            var cards = pile.Cards;
            int start = pile.Count - 13; // início do possível run
            int end = pile.Count - 1;    // topo da coluna

            var first = cards[start];
            var last = cards[end];

            // tem de estar tudo virado para cima e ser K..A do mesmo naipe
            if (!first.IsFaceUp || !last.IsFaceUp)
                break;

            if (first.Rank != Rank.King || last.Rank != Rank.Ace)
                break;

            Suit suit = first.Suit;
            bool isRun = true;

            for (int i = start; i < end; i++)
            {
                var current = cards[i];
                var next = cards[i + 1];

                if (!current.IsFaceUp || !next.IsFaceUp)
                {
                    isRun = false;
                    break;
                }

                if (current.Suit != suit || next.Suit != suit)
                {
                    isRun = false;
                    break;
                }

                if ((int)current.Rank != (int)next.Rank + 1)
                {
                    // tem de ser descendente contínuo: K,Q,J,10,...,2,A
                    isRun = false;
                    break;
                }
            }

            if (!isRun)
                break;

            // remover o run completo da pilha
            pile.TakeRange(start, 13);
            CompletedRuns++;
            removed = true;

            // vira a nova carta de topo (se existir e estiver virada para baixo)
            if (pile.TopCard is Card newTop && !newTop.IsFaceUp)
            {
                newTop.IsFaceUp = true;
            }

        } while (removed && pile.Count >= 13);
    }
    #endregion

    #region ...[Is Complete Run]...
    private bool IsCompleteRun(IReadOnlyList<Card> run)
    {
        if (run.Count != 13) return false;

        // todas do mesmo naipe
        if (run.Any(c => c.Suit != run[0].Suit)) return false;

        // começa em King e termina em Ace
        if (run[0].Rank != Rank.King || run[^1].Rank != Rank.Ace) return false;

        return IsValidDescendingSequence(run);
    }
    #endregion

    #region ...[Is Game Won]...
    public bool IsGameWon()
    {
        // 8 sequências completas em Spider normal
        return CompletedRuns >= 8;
    }
    #endregion

    #region ...[Util]
    private static int NormalizeSuitCount(int suitCount)
        => suitCount switch
        {
            1 => 1,
            2 => 2,
            4 => 4,
            _ => 1
        };
    #endregion

    #region ...[Get Movable Sequence End]...
    public int GetMovableSequenceEnd(int pileIndex, int startIndex)
    {
        var cards = Piles[pileIndex].Cards;

        if (startIndex < 0 || startIndex >= cards.Count)
            return startIndex;

        var startCard = cards[startIndex];
        if (!startCard.IsFaceUp)
            return startIndex;

        int i = startIndex;

        while (i < cards.Count - 1)
        {
            var current = cards[i];
            var next = cards[i + 1];

            if (!next.IsFaceUp)
                break;

            // descendente contínuo (ex.: 7 sobre 8, 6 sobre 7)
            if ((int)current.Rank != (int)next.Rank + 1)
                break;

            // mesmo naipe
            if (current.Suit != next.Suit)
                break;

            i++;
        }

        // devolve o índice da última carta que ainda pertence ao bloco movível
        return i;
    }
    #endregion

    #region ...[Has Any Tableau Move]...
    private bool HasAnyTableauMove()
    {
        // percorre todas as colunas
        for (int fromPileIndex = 0; fromPileIndex < Piles.Count; fromPileIndex++)
        {
            var fromPile = Piles[fromPileIndex];
            var cards = fromPile.Cards;

            int lastIndex = cards.Count - 1;
            if (lastIndex < 0)
                continue;

            for (int i = 0; i <= lastIndex; i++)
            {
                var card = cards[i];
                if (!card.IsFaceUp) continue;

                int endIndex = GetMovableSequenceEnd(fromPileIndex, i);
                if (i != lastIndex && endIndex != lastIndex)
                    continue;

                var first = cards[i];

                for (int toPileIndex = 0; toPileIndex < Piles.Count; toPileIndex++)
                {
                    if (toPileIndex == fromPileIndex) continue;

                    var toPile = Piles[toPileIndex];

                    if (toPile.Count == 0)
                        return true;

                    var top = toPile.TopCard;
                    if (top is null || !top.IsFaceUp)
                        continue;

                    if ((int)top.Rank == (int)first.Rank + 1)
                        return true;
                }
            }
        }

        return false;
    }
    #endregion

    #region ...[Has Any Sequence Move]...
    private bool HasAnySequenceMove()
    {
        // percorre todas as colunas de origem
        for (int fromPileIndex = 0; fromPileIndex < Piles.Count; fromPileIndex++)
        {
            var fromPile = Piles[fromPileIndex];
            var cards = fromPile.Cards;

            int lastIndex = cards.Count - 1;
            if (lastIndex < 0)
                continue;

            for (int i = 0; i <= lastIndex; i++)
            {
                var card = cards[i];
                if (!card.IsFaceUp) continue;

                int endIndex = GetMovableSequenceEnd(fromPileIndex, i);
                if (i != lastIndex && endIndex != lastIndex)
                    continue;

                int count = endIndex - i + 1;
                if (count <= 1)
                    continue; // só interessa bloco >= 2

                var first = cards[i];

                for (int toPileIndex = 0; toPileIndex < Piles.Count; toPileIndex++)
                {
                    if (toPileIndex == fromPileIndex) continue;

                    var toPile = Piles[toPileIndex];

                    if (toPile.Count == 0)
                        return true;

                    var top = toPile.TopCard;
                    if (top is null || !top.IsFaceUp)
                        continue;

                    if ((int)top.Rank == (int)first.Rank + 1)
                        return true;
                }
            }
        }

        return false;
    }
    #endregion

    #region ...[Is Game Lost]...
    public bool IsGameLost()
    {
        // se já ganhou, não está perdido
        if (IsGameWon())
            return false;

        // se ainda pode dar cartas do stock, consideramos que ainda há esperança
        if (CanDealFromStock())
            return false;

        // se existir pelo menos UM movimento que envolva 2+ cartas (sequência),
        // não consideramos perdido
        if (HasAnySequenceMove())
            return false;

        // aqui chegamos a:
        // - sem stock
        // - e ou não há movimentos nenhuns
        //   ou só existem movimentos de 1 carta
        return true;
    }
    #endregion
}
