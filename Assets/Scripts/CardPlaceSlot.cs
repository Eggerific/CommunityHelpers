using UnityEngine;

public class CardPlaceSlot : MonoBehaviour
{
    // This variable determines what the base card is and what cards can go on top of it.
    // If a card's cardCategory = the spot's cardCategory, it can be placed there.
    // An empty cardCategory accepts any card.
    public string cardCategory;

    // Get collider
    private BoxCollider boxCollider;

    // Cards in stack are children of this game object

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
    }

    public bool Accepts(CardScript card) =>
        string.IsNullOrEmpty(cardCategory) || card.cardCategory == cardCategory;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Card")) return;
        var card = other.GetComponent<CardScript>();
        if (card != null && Accepts(card)) card.SetPull(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Card")) return;
        // Slots overlap (the cards are stacked with an offset), so only let go of a card this slot is pulling.
        var card = other.GetComponent<CardScript>();
        if (card != null && card.slotPull == this) card.ResetPull();
    }
}
