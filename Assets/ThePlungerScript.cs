using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Color = ThePlunger.Color;
using rnd = UnityEngine.Random;

public class ThePlungerScript : MonoBehaviour
{
    public KMNeedyModule Module;
    public KMAudio Audio;
    public KMBombInfo BombInfo;

    public KMSelectable PlungerSelectable;
    public MeshRenderer PlungerRenderer;
    public List<Material> Materials; //Red, Green, BLue, Yellow
    public Animator ButtonAnimator;
    public TextMesh ButtonText;

    public KMColorblindMode ColorblindMode;
    public TextMesh ColorBlindText;

    private static readonly List<string> _texts = new List<string>
    {
        "Foe",
        "Though",
        "Neat",
        "Need"
    };
    private static readonly int[,] _textValues = new int[,]
    {
        {7, 5, 3, 3},
        {6, 7, 6, 2},
        {1, 4, 1, 7},
        {9, 5, 9, 0},
        {2, 2, 3, 7}
    };
    private static readonly int[,] _colorValues = new int[,]
    {
        {3, 3, 5, 9},
        {2, 8, 9, 7},
        {0, 4, 5, 6},
        {1, 5, 2, 1},
        {3, 4, 4, 7}
    };

    private bool _colorBlind;
    private bool _isActive;
    private int _activationCount;
    private int _answer;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private static readonly int _plungerTrigger = Animator.StringToHash("PlungerTrigger");
    
    void Start()
    {
        _moduleId = _moduleIdCounter++;
        Module.OnNeedyActivation += OnNeedyActication;
        Module.OnTimerExpired += OnTimerExpired;
        PlungerSelectable.OnInteract += () =>
        {
            OnPress();
            return false;
        };

        if (ColorblindMode.ColorblindModeActive)
        {
            _colorBlind = true;
            ColorBlindText.gameObject.SetActive(true);
        }
    }

    private void OnTimerExpired()
    {
        Module.HandleStrike();
        ButtonText.text = string.Empty;
        _isActive = false;
    }

    private void OnNeedyActication()
    {
        var color = (Color) rnd.Range(0, 4);
        var text = rnd.Range(0, _texts.Count);

        PlungerRenderer.material = Materials[(int) color];
        ButtonText.text = _texts[text];

        _activationCount = _activationCount == 5 ? 1 : _activationCount + 1;
        var colorValue = _colorValues[_activationCount - 1, (int) color];
        var textValue = _textValues[_activationCount - 1, text];
        ButtonText.color = color == Color.Yellow ? UnityEngine.Color.black : UnityEngine.Color.white;
        ColorBlindText.text = color.ToString();

        _answer = (colorValue + textValue) % 10;
        DebugLog("Activation #{0}, the color is {1}, the text is {2}, which results in ({3} + {4}) % 10 which is {5}. Expecting the plunger to be pressed on a {5}.", _activationCount, 
            color.ToString(), _texts[text], colorValue, textValue, _answer);
        _isActive = true;
    }
    
    private void OnPress()
    {
        ButtonAnimator.SetTrigger(_plungerTrigger);
        PlungerSelectable.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, PlungerSelectable.transform);
        if (_isActive)
        {
            var bombTime = (int) BombInfo.GetTime() % 10;
            if (bombTime == _answer)
            {
                DebugLog("You pressed the button at {0}, which is correct. Module disarmed.", bombTime);
                _isActive = false;
                Module.HandlePass();
                ButtonText.text = string.Empty;
                return;
            }
            DebugLog("You pressed the button on a {0}, when I expected it to be pressed on a {1}. Strike!", bombTime, _answer);
            Module.HandleStrike();
            Module.HandlePass();
            ButtonText.text = string.Empty;
            _isActive = false;
        }
    }

    private void DebugLog(string message, params object[] args)
    {
        Debug.LogFormat("[The Plunger #{0}] {1}", _moduleId, string.Format(message, args));
    }

    private static readonly Regex TpRegex = new Regex("^press on (\\d)$");
#pragma warning disable 414
    private const string TwitchHelpMessage = "Press on the plunger on a specific time using: !{0} press on #. Activate colorblind mode using !{0} colorblind.";
#pragma warning restore 414    
    
    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant().Trim();
        var m = TpRegex.Match(command);
        if (command.EqualsAny("colorblind", "cb", "color blind", "colourblind", "colour blind"))
        {
            yield return null;
            if (_colorBlind)
            {
                yield return "sendtochat Colorblind mode is already active!";
                yield break;
            }
            
            yield return "sendtochat Colorblind mode is now active!";
            _colorBlind = true;
            ColorBlindText.gameObject.SetActive(true);
            yield break;
        }
        if (m.Success)
        {
            yield return null;
            while ((int)BombInfo.GetTime() % 10 == int.Parse(m.Groups[1].ToString()))
                yield return "trycancel The plunger was not pressed due to a request to cancel.";
            while ((int)BombInfo.GetTime() % 10 != int.Parse(m.Groups[1].ToString()))
                yield return "trycancel The plunger was not pressed due to a request to cancel.";
            PlungerSelectable.OnInteract();
        }
    }

    private void TwitchHandleForcedSolve()
    {
        StartCoroutine(ForceSolve());
    }

    private IEnumerator ForceSolve()
    {
        while (true)
        {
            if ((int) BombInfo.GetTime() % 10 == _answer && _isActive)
                PlungerSelectable.OnInteract();
            yield return null;
        }
    }
}