namespace SpiderGame.Game;

public class Card
{
    public Suit Suit { get; }
    public Rank Rank { get; }
    public bool IsFaceUp { get; set; }

    public Card(Suit suit, Rank rank, bool isFaceUp = false)
    {
        Suit = suit;
        Rank = rank;
        IsFaceUp = isFaceUp;
    }
}
