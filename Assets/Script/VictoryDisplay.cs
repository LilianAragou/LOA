using UnityEngine;
using TMPro;

public class VictoryDisplay : MonoBehaviour
{
    public TextMeshProUGUI finalText;
    void Start() =>
    finalText.text = GameResultData.VictoryMessage;

}
