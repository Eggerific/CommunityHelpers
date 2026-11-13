using UnityEngine;

public class CardPlaceSlot : MonoBehaviour
{
    // This variable determines what the base card is and what cards can go on top of it.
    // If a card's cardCategory = the spot's cardCategory, it can be placed there
    public string cardCategory;

    // Get collider
    private BoxCollider boxCollider;

    //private GameObject cardCandidate = null;

    // Cards in stack are children of this game object

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Card"))
        {
            //cardCandidate = other.gameObject;
            other.gameObject.GetComponent<CardScript>().slotPull = this;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Bye!");
        if (other.CompareTag("Card"))
        {
            //cardCandidate = null;
            other.gameObject.GetComponent<CardScript>().slotPull = null;
        }
    }
}
