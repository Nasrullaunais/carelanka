import 'package:flutter/material.dart';

/// The look of the whole app, in one place.
///
/// Shared, so a screen never hard-codes a colour or a corner radius. If you
/// find yourself writing `Color(0x...)` in a feature, the value belongs here.
class AppTheme {
  const AppTheme._();

  /// A calm blue-green. Hospital apps avoid red and orange as brand colours —
  /// those have to stay free to mean "urgent" wherever they appear.
  static const _seed = Color(0xFF00695C);

  static ThemeData get light => _build(Brightness.light);
  static ThemeData get dark => _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    final scheme = ColorScheme.fromSeed(seedColor: _seed, brightness: brightness);
    final base = ThemeData(colorScheme: scheme, useMaterial3: true);

    return base.copyWith(
      scaffoldBackgroundColor: brightness == Brightness.light
          ? const Color(0xFFF6F8F8)
          : scheme.surface,
      textTheme: _text(base.textTheme),
      appBarTheme: AppBarTheme(
        backgroundColor: Colors.transparent,
        surfaceTintColor: Colors.transparent,
        scrolledUnderElevation: 0,
        centerTitle: false,
        titleTextStyle: _text(base.textTheme).titleLarge?.copyWith(
              color: scheme.onSurface,
              fontWeight: FontWeight.w600,
            ),
      ),
      cardTheme: CardThemeData(
        elevation: 0,
        margin: EdgeInsets.zero,
        color: scheme.surface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(radiusL),
          side: BorderSide(color: scheme.outlineVariant.withValues(alpha: 0.6)),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: brightness == Brightness.light
            ? Colors.white
            : scheme.surfaceContainerHighest,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        border: _inputBorder(scheme.outlineVariant),
        enabledBorder: _inputBorder(scheme.outlineVariant),
        focusedBorder: _inputBorder(scheme.primary, width: 2),
        errorBorder: _inputBorder(scheme.error),
        focusedErrorBorder: _inputBorder(scheme.error, width: 2),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusM)),
          textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusM)),
          side: BorderSide(color: scheme.outlineVariant),
        ),
      ),
      navigationBarTheme: NavigationBarThemeData(
        height: 68,
        elevation: 0,
        backgroundColor: brightness == Brightness.light ? Colors.white : scheme.surface,
        surfaceTintColor: Colors.transparent,
        indicatorColor: scheme.primaryContainer,
        labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
        labelTextStyle: WidgetStatePropertyAll(
          TextStyle(fontSize: 12, fontWeight: FontWeight.w500, color: scheme.onSurface),
        ),
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusM)),
      ),
      bottomSheetTheme: const BottomSheetThemeData(
        showDragHandle: true,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: Radius.circular(radiusXL)),
        ),
      ),
      dividerTheme: DividerThemeData(color: scheme.outlineVariant, space: 1, thickness: 1),
      listTileTheme: const ListTileThemeData(
        contentPadding: EdgeInsets.symmetric(horizontal: 20, vertical: 4),
      ),
    );
  }

  static TextTheme _text(TextTheme base) => base.copyWith(
        headlineSmall: base.headlineSmall?.copyWith(fontWeight: FontWeight.w600, height: 1.2),
        titleLarge: base.titleLarge?.copyWith(fontWeight: FontWeight.w600),
        titleMedium: base.titleMedium?.copyWith(fontWeight: FontWeight.w600),
        titleSmall: base.titleSmall?.copyWith(fontWeight: FontWeight.w600),
        bodyMedium: base.bodyMedium?.copyWith(height: 1.45),
        bodySmall: base.bodySmall?.copyWith(height: 1.4),
        labelLarge: base.labelLarge?.copyWith(fontWeight: FontWeight.w600),
      );

  static OutlineInputBorder _inputBorder(Color color, {double width = 1}) => OutlineInputBorder(
        borderRadius: BorderRadius.circular(radiusM),
        borderSide: BorderSide(color: color, width: width),
      );

  static const radiusS = 8.0;
  static const radiusM = 12.0;
  static const radiusL = 16.0;
  static const radiusXL = 24.0;

  /// The gutter every screen uses down both edges.
  static const gutter = 20.0;
}

/// Colours that carry a meaning rather than a brand.
///
/// Deliberately short. This app has **three** signals beyond the brand teal:
/// something needs you (amber), something went wrong (the scheme's own error
/// red), and something is finished (grey). Everything else is teal or neutral.
///
/// A blue "info" colour was tried and removed — sitting next to teal it read as
/// a second brand colour, and a reader cannot tell two accent colours apart if
/// neither is obviously more urgent. Where information needs highlighting,
/// use `primary` on a neutral surface.
extension StatusColors on ColorScheme {
  /// Waiting on somebody, or missing something. The only colour in the app
  /// that competes with the brand for attention, so it is used sparingly.
  Color get warning =>
      brightness == Brightness.light ? const Color(0xFF8A5A00) : const Color(0xFFE8BE7E);

  /// A tint, not a slab — meant to sit behind an icon, not a whole card.
  Color get warningSurface =>
      brightness == Brightness.light ? const Color(0xFFFBF2E3) : const Color(0xFF3A2606);

  /// Done, closed, historical. Deliberately colourless.
  Color get muted => onSurfaceVariant;

  Color get mutedSurface => surfaceContainerHighest;
}
