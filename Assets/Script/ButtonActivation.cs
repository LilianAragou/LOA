using UnityEngine;

public class ButtonActivation : MonoBehaviour
{
    public GameObject panelDisappear;
    public GameObject panelAppear;
    public GameObject buttonDisappear;
    public GameObject buttonAppear;

    public void OnButtonClick()
    {
        if (panelDisappear != null) 
        {
            panelDisappear.SetActive(false);
        }
        if (panelAppear != null)
        {
            panelAppear.SetActive(true);
        }
        if (buttonDisappear != null)
        {
            buttonDisappear.SetActive(false);
        }
        if (buttonAppear != null)
        {
            buttonAppear.SetActive(true);
        }
    }
}
