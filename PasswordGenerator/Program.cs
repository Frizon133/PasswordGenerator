using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace PasswordGenerator;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new PasswordForm());
    }
}

public sealed class PasswordForm : Form
{
    private static readonly Color Background = Color.FromArgb(19, 21, 29);
    private static readonly Color Surface = Color.FromArgb(29, 32, 43);
    private static readonly Color Foreground = Color.FromArgb(239, 240, 249);
    private static readonly Color Muted = Color.FromArgb(163, 170, 192);
    private static readonly Color Accent = Color.FromArgb(186, 167, 255);
    private static readonly Color Green = Color.FromArgb(126, 223, 182);
    private static readonly Color Amber = Color.FromArgb(246, 193, 119);

    private readonly TextBox passwordBox;
    private readonly NumericUpDown lengthInput;
    private readonly TrackBar lengthSlider;
    private readonly Label lengthHint;
    private readonly Label status;
    private readonly Button copyButton;

    private readonly CheckBox checkUppercase;
    private readonly CheckBox checkLowercase;
    private readonly CheckBox checkDigits;
    private readonly CheckBox checkSymbols;

    private bool updatingLength;

    private static readonly string[] Groups =
    {
        "abcdefghijklmnopqrstuvwxyz",
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
        "0123456789",
        "!@#$%^&*()-_=+[]{};:,.?/|~"
    };

    private static readonly string Alphabet = string.Concat(Groups);

    public PasswordForm()
    {
        Text = "Генератор паролей";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(640, 568);
        BackColor = Background;
        ForeColor = Foreground;
        Font = new Font("Segoe UI", 10);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        AddLabel(this, "Генератор паролей",
            28, 24, 584, 40, 24, Foreground, true);

        AddLabel(this, "Ваш новый пароль — в один клик.",
            30, 72, 580, 24, 11, Muted);

        // Карточка с паролем.
        var passwordCard = new RoundedPanel
        {
            Bounds = new Rectangle(28, 112, 584, 140),
            BackColor = Surface
        };
        Controls.Add(passwordCard);

        AddLabel(passwordCard, "ВАШ ПАРОЛЬ",
            22, 16, 260, 22, 9, Muted, true);

        passwordBox = new TextBox
        {
            Bounds = new Rectangle(22, 52, 540, 34),
            BackColor = Surface,
            ForeColor = Foreground,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 19),
            ReadOnly = true,
            AccessibleName = "Сгенерированный пароль"
        };
        passwordCard.Controls.Add(passwordBox);

        lengthHint = AddLabel(passwordCard, "",
            22, 103, 540, 22, 10, Green);

        // Карточка с настройками.
        var settingsCard = new RoundedPanel
        {
            Bounds = new Rectangle(28, 268, 584, 176),
            BackColor = Surface
        };
        Controls.Add(settingsCard);

        AddLabel(settingsCard, "Длина пароля",
            22, 20, 300, 26, 12, Foreground, true);

        lengthInput = new NumericUpDown
        {
            Bounds = new Rectangle(470, 18, 92, 30),
            Minimum = 4,
            Maximum = 128,
            Value = 20,
            BackColor = Surface,
            ForeColor = Accent,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Center,
            AccessibleName = "Длина пароля"
        };
        settingsCard.Controls.Add(lengthInput);

        lengthSlider = new TrackBar
        {
            Bounds = new Rectangle(14, 58, 556, 35),
            Minimum = 4,
            Maximum = 128,
            Value = 20,
            SmallChange = 1,
            LargeChange = 4,
            TickStyle = TickStyle.None,
            AutoSize = false,
            BackColor = Surface,
            AccessibleName = "Выбрать длину пароля"
        };
        settingsCard.Controls.Add(lengthSlider);

        AddLabel(settingsCard, "4 символа",
            22, 94, 150, 22, 9, Muted);

        var maximumLabel = AddLabel(settingsCard, "128 символов",
            392, 94, 170, 22, 9, Muted);
        maximumLabel.TextAlign = ContentAlignment.MiddleRight;

        string[] badges =
        {
            "A–Z  Заглавные",
            "a–z  Строчные",
            "0–9  Цифры",
            "#$&  Символы"
        };

        int[] checkPositions = { 22, 159, 296, 433 };

        checkUppercase = AddCheckBox(settingsCard, badges[0], checkPositions[0], 130, true);
        checkLowercase = AddCheckBox(settingsCard, badges[1], checkPositions[1], 130, true);
        checkDigits = AddCheckBox(settingsCard, badges[2], checkPositions[2], 130, true);
        checkSymbols = AddCheckBox(settingsCard, badges[3], checkPositions[3], 130, true);

        checkUppercase.CheckedChanged += (_, _) => GeneratePassword();
        checkLowercase.CheckedChanged += (_, _) => GeneratePassword();
        checkDigits.CheckedChanged += (_, _) => GeneratePassword();
        checkSymbols.CheckedChanged += (_, _) => GeneratePassword();

        // Кнопки действий.
        var generateButton = MakeButton(
            "Сгенерировать пароль",
            new Rectangle(28, 462, 370, 48),
            true);

        copyButton = MakeButton(
            "Копировать",
            new Rectangle(412, 462, 200, 48),
            false);

        Controls.AddRange(new Control[] { generateButton, copyButton });
        AcceptButton = generateButton;

        status = AddLabel(this,
            "Рекомендуем от 16 символов. Все 4 типа включены.",
            28, 526, 584, 24, 9, Muted);

        status.TextAlign = ContentAlignment.MiddleCenter;

        lengthInput.ValueChanged += (_, _) =>
            SetLength((int)lengthInput.Value);

        lengthSlider.ValueChanged += (_, _) =>
            SetLength(lengthSlider.Value);

        generateButton.Click += (_, _) => GeneratePassword();
        copyButton.Click += (_, _) => CopyPassword();

        GeneratePassword();
    }

    private void SetLength(int length)
    {
        // Защита от повторного вызова событий при синхронизации.
        if (updatingLength)
            return;

        updatingLength = true;

        try
        {
            lengthInput.Value = length;
            lengthSlider.Value = length;
        }
        finally
        {
            updatingLength = false;
        }

        GeneratePassword();
    }

    private void GeneratePassword()
    {
        int length = (int)lengthInput.Value;

        // Проверяем, что выбран хотя бы один тип символов
        if (!checkUppercase.Checked && !checkLowercase.Checked && 
            !checkDigits.Checked && !checkSymbols.Checked)
        {
            passwordBox.Text = string.Empty;
            lengthHint.Text = "Выберите хотя бы один тип символов";
            lengthHint.ForeColor = Amber;
            status.Text = "Ошибка: нужно выбрать тип символов";
            status.ForeColor = Amber;
            copyButton.Text = "Копировать";
            return;
        }

        passwordBox.Text = CreatePassword(length);

        passwordBox.SelectionStart = 0;
        passwordBox.SelectionLength = 0;
        passwordBox.ScrollToCaret();

        lengthHint.Text = length >= 16
            ? $"Длина: {length}  ·  Рекомендация по длине выполнена"
            : $"Длина: {length}  ·  Лучше использовать от 16 символов";

        lengthHint.ForeColor = length >= 16 ? Green : Amber;

        copyButton.Text = "Копировать";

        int selectedTypes = (checkUppercase.Checked ? 1 : 0) + 
                            (checkLowercase.Checked ? 1 : 0) + 
                            (checkDigits.Checked ? 1 : 0) + 
                            (checkSymbols.Checked ? 1 : 0);

        status.Text = $"Выбрано типов: {selectedTypes} из 4";
        status.ForeColor = Muted;
    }

    private string CreatePassword(int length)
    {
        if (length < 4 || length > 128)
            throw new ArgumentOutOfRangeException(nameof(length));

        // Собираем доступные группы символов
        var availableGroups = new List<string>();
        if (checkLowercase.Checked)
            availableGroups.Add(Groups[0]); // строчные
        if (checkUppercase.Checked)
            availableGroups.Add(Groups[1]); // ЗАГЛАВНЫЕ
        if (checkDigits.Checked)
            availableGroups.Add(Groups[2]); // цифры
        if (checkSymbols.Checked)
            availableGroups.Add(Groups[3]); // символы

        if (availableGroups.Count == 0)
            throw new InvalidOperationException("Нет выбранных типов символов");

        string alphabet = string.Concat(availableGroups);
        var characters = new char[length];

        do
        {
            for (int i = 0; i < characters.Length; i++)
            {
                int index = RandomNumberGenerator.GetInt32(alphabet.Length);
                characters[i] = alphabet[index];
            }
        }
        while (availableGroups.Count > 1 && 
               !availableGroups.All(group => 
                   characters.Any(character => group.Contains(character))));

        return new string(characters);
    }

    private void CopyPassword()
    {
        if (string.IsNullOrEmpty(passwordBox.Text))
            return;

        try
        {
            Clipboard.SetText(passwordBox.Text);
            copyButton.Text = "Скопировано";
            status.Text = "Пароль скопирован в буфер обмена.";
            status.ForeColor = Green;
        }
        catch (ExternalException)
        {
            status.Text = "Буфер обмена занят. Попробуйте ещё раз.";
            status.ForeColor = Amber;
        }
    }

    private static Label AddLabel(
        Control parent, string text,
        int x, int y, int width, int height,
        float fontSize, Color color, bool bold = false)
    {
        var label = new Label
        {
            Text = text,
            Bounds = new Rectangle(x, y, width, height),
            Font = new Font(
                "Segoe UI",
                fontSize,
                bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = color,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        };

        parent.Controls.Add(label);
        return label;
    }

    private CheckBox AddCheckBox(
        Control parent, string text,
        int x, int y, bool isChecked)
    {
        var checkBox = new CheckBox
        {
            Text = text,
            Bounds = new Rectangle(x, y, 129, 28),
            Font = new Font("Segoe UI", 9),
            ForeColor = Accent,
            BackColor = Color.FromArgb(42, 43, 61),
            Checked = isChecked,
            TextAlign = ContentAlignment.MiddleCenter,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false
        };

        checkBox.FlatAppearance.BorderColor = Color.FromArgb(66, 70, 90);
        checkBox.FlatAppearance.BorderSize = 1;
        checkBox.FlatAppearance.CheckedBackColor = Accent;
        checkBox.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 57, 76);

        parent.Controls.Add(checkBox);
        return checkBox;
    }

    private static Button MakeButton(
        string text, Rectangle bounds, bool primary)
    {
        var button = new Button
        {
            Text = text,
            Bounds = bounds,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : Surface,
            ForeColor = primary ? Background : Foreground,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };

        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(66, 70, 90);

        button.FlatAppearance.MouseOverBackColor = primary
            ? Color.FromArgb(204, 190, 255)
            : Color.FromArgb(43, 47, 63);

        button.FlatAppearance.MouseDownBackColor = primary
            ? Color.FromArgb(163, 142, 237)
            : Color.FromArgb(52, 57, 76);

        return button;
    }
}

// Панель с закруглёнными углами.
public sealed class RoundedPanel : Panel
{
    public RoundedPanel()
    {
        DoubleBuffered = true;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);

        const int diameter = 28;

        if (Width < diameter || Height < diameter)
            return;

        using var path = new GraphicsPath();

        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(Width - diameter, 0, diameter, diameter, 270, 90);
        path.AddArc(
            Width - diameter, Height - diameter,
            diameter, diameter, 0, 90);
        path.AddArc(0, Height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        var previousRegion = Region;
        Region = new Region(path);
        previousRegion?.Dispose();
    }
}