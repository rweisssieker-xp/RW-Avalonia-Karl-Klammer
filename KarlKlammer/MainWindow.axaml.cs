using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace KarlKlammer;

public partial class MainWindow : Window
{
    private readonly TranslateTransform _bobTransform;

    private readonly string[] _tips =
    [
        "Hallo! Ich bin Karl Klammer – der Büroklammer-Assistent aus alten Microsoft-Zeiten. Klick auf „Noch ein Tipp…“ für mehr Weisheit.",
        "Zum Verschieben: Linke Maustaste auf die gelbe Sprechblase oder die Klammer halten und ziehen.",
        "Früher habe ich in Word und Excel vorgeschlagen, ob du Hilfe brauchst – ob du wolltest oder nicht.",
        "Wenn du feststeckst: Problem in einem Satz aufschreiben. Oft sieht man dann schon die nächste kleine Schrittfolge.",
        "In Deutschland hieß ich „Karl Klammer“; international war ich „Clippy“. Gleiche Klammer, andere Namen.",
        "Kleiner Trick: Erst die einfachste Version bauen, dann erst schöner machen. Das erspart viel Frust.",
    ];

    private int _tipIndex;
    private readonly DispatcherTimer _bobTimer;
    private double _bobPhase;

    public MainWindow()
    {
        InitializeComponent();
        _bobTransform = (TranslateTransform)ClipViewbox.RenderTransform!;
        _tipIndex = Random.Shared.Next(_tips.Length);
        HintText.Text = _tips[_tipIndex];

        _bobTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
        _bobTimer.Tick += (_, _) =>
        {
            _bobPhase += 0.11;
            _bobTransform.Y = Math.Sin(_bobPhase) * 5;
        };
        _bobTimer.Start();
    }

    private void OnDragPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnNextTipClick(object? sender, RoutedEventArgs e)
    {
        _tipIndex = (_tipIndex + 1) % _tips.Length;
        HintText.Text = _tips[_tipIndex];
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
