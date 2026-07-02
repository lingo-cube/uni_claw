namespace UniClaw.Core.Domain.Mappings;

/// <summary>
/// Android widget class name 枚举（PRD §5.4: ported from element_type_mapper.py）。
/// 每个成员的值是对应的完整 Android 类路径。
/// </summary>
public enum AndroidWidgetClass
{
    /// <summary>android.widget.Switch</summary>
    Switch,
    /// <summary>android.widget.CheckBox</summary>
    CheckBox,
    /// <summary>android.widget.RadioButton</summary>
    RadioButton,
    /// <summary>android.widget.ToggleButton</summary>
    ToggleButton,
    /// <summary>android.widget.Button</summary>
    Button,
    /// <summary>android.widget.ImageButton</summary>
    ImageButton,
    /// <summary>android.widget.TextView</summary>
    TextView,
    /// <summary>android.widget.EditText</summary>
    EditText,
    /// <summary>android.widget.LinearLayout</summary>
    LinearLayout,
    /// <summary>android.widget.RelativeLayout</summary>
    RelativeLayout,
    /// <summary>android.widget.FrameLayout</summary>
    FrameLayout,
    /// <summary>androidx.constraintlayout.widget.ConstraintLayout</summary>
    ConstraintLayout,
    /// <summary>android.widget.SeekBar</summary>
    SeekBar,
    /// <summary>android.widget.RatingBar</summary>
    RatingBar
}
