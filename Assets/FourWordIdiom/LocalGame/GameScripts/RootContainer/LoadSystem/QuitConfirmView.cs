using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class QuitConfirmView : UIWindow
{
    [SerializeField] private Button _quitConfirmButton;
    [SerializeField] private Button _cancelButton;
    // Start is called before the first frame update

    private void Start()
    {
        _quitConfirmButton.AddClickAction(OnQuitConfirm);
        _cancelButton.AddClickAction(OnCancel);
    }

    private void OnCancel()
    {
        this.Close();
        SystemManager.Instance.HidePanel(PanelType.QuitConfirmView);
    }

    private void OnQuitConfirm()
    {
        Application.Quit();
    }
}