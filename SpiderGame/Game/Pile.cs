using System.Collections.Generic;
using System.Linq;

namespace SpiderGame.Game;

public class Pile
{
    private readonly List<Card> _cards = new();

    public IReadOnlyList<Card> Cards => _cards;

    public void AddCard(Card card) => _cards.Add(card);

    public void AddCards(IEnumerable<Card> cards) => _cards.AddRange(cards);

    public Card? TopCard => _cards.Count > 0 ? _cards[^1] : null;

    public IEnumerable<Card> GetFaceUpSequenceFrom(int index)
    {
        // devolve as cartas da posição "index" até ao fim
        return _cards.Skip(index);
    }

    // 🔹 NOVO: devolve um bloco [index..index+count-1] e remove-o da pilha
    public List<Card> TakeRange(int index, int count)
    {
        var range = _cards.GetRange(index, count);
        _cards.RemoveRange(index, count);
        return range;
    }

    public int Count => _cards.Count;
}

