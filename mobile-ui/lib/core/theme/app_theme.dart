import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

class AppTheme {
  const AppTheme._();

  // A more modern, trustworthy healthcare blue (Cerulean/Ocean Blue)
  static const _seed = Color(0xFF0284C7);

  static ThemeData get light => _build(Brightness.light);
  static ThemeData get dark => _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    final scheme = ColorScheme.fromSeed(
      seedColor: _seed,
      brightness: brightness,
      primary: _seed,
      surfaceTint: Colors.transparent,
    );
    final base = ThemeData(colorScheme: scheme, useMaterial3: true);

    return base.copyWith(
      scaffoldBackgroundColor: brightness == Brightness.light
          ? const Color(0xFFF8FAFC) // Very clean, cool white/grey
          : const Color(0xFF0F172A), // Sleek slate dark mode
      textTheme: _text(base.textTheme),
      appBarTheme: AppBarTheme(
        backgroundColor: Colors.transparent,
        surfaceTintColor: Colors.transparent,
        scrolledUnderElevation: 0,
        centerTitle: false,
        titleTextStyle: _text(base.textTheme).titleLarge?.copyWith(
              color: scheme.onSurface,
              fontWeight: FontWeight.w700,
            ),
      ),
      cardTheme: CardThemeData(
        elevation: brightness == Brightness.light ? 8 : 0, // Subtle floating effect in light mode
        shadowColor: scheme.shadow.withValues(alpha: 0.08),
        margin: EdgeInsets.zero,
        color: brightness == Brightness.light ? Colors.white : scheme.surfaceContainer,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(radiusL),
          side: BorderSide(
            color: brightness == Brightness.light 
              ? Colors.transparent 
              : scheme.outlineVariant.withValues(alpha: 0.3),
          ),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: brightness == Brightness.light
            ? Colors.white
            : scheme.surfaceContainerHighest,
        contentPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 18),
        border: _inputBorder(scheme.outlineVariant.withValues(alpha: 0.5)),
        enabledBorder: _inputBorder(scheme.outlineVariant.withValues(alpha: 0.5)),
        focusedBorder: _inputBorder(scheme.primary, width: 2),
        errorBorder: _inputBorder(scheme.error),
        focusedErrorBorder: _inputBorder(scheme.error, width: 2),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(56),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusL)),
          textStyle: GoogleFonts.outfit(fontSize: 16, fontWeight: FontWeight.w600),
          elevation: 0,
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size.fromHeight(56),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusL)),
          side: BorderSide(color: scheme.outlineVariant.withValues(alpha: 0.8)),
          textStyle: GoogleFonts.outfit(fontSize: 16, fontWeight: FontWeight.w600),
        ),
      ),
      navigationBarTheme: NavigationBarThemeData(
        height: 72,
        elevation: 0,
        backgroundColor: brightness == Brightness.light ? Colors.white : scheme.surface,
        surfaceTintColor: Colors.transparent,
        indicatorColor: scheme.primaryContainer,
        labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
        labelTextStyle: WidgetStatePropertyAll(
          GoogleFonts.outfit(fontSize: 12, fontWeight: FontWeight.w500, color: scheme.onSurface),
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
      dividerTheme: DividerThemeData(
        color: scheme.outlineVariant.withValues(alpha: 0.4), 
        space: 1, 
        thickness: 1,
      ),
      listTileTheme: ListTileThemeData(
        contentPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 8),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusM)),
      ),
    );
  }

  static TextTheme _text(TextTheme base) {
    // Apply Outfit font for a very modern, rounded, clean look
    final textTheme = GoogleFonts.outfitTextTheme(base);
    return textTheme.copyWith(
      headlineSmall: textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700, height: 1.2),
      titleLarge: textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700),
      titleMedium: textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w600),
      titleSmall: textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w600),
      bodyMedium: textTheme.bodyMedium?.copyWith(height: 1.5, color: textTheme.bodyMedium?.color?.withValues(alpha: 0.8)),
      bodySmall: textTheme.bodySmall?.copyWith(height: 1.4, color: textTheme.bodySmall?.color?.withValues(alpha: 0.7)),
      labelLarge: textTheme.labelLarge?.copyWith(fontWeight: FontWeight.w600),
    );
  }

  static OutlineInputBorder _inputBorder(Color color, {double width = 1}) => OutlineInputBorder(
        borderRadius: BorderRadius.circular(radiusM),
        borderSide: BorderSide(color: color, width: width),
      );

  // Increased radii for a softer, more modern appearance
  static const radiusS = 12.0;
  static const radiusM = 16.0;
  static const radiusL = 24.0;
  static const radiusXL = 32.0;

  static const gutter = 24.0;
}

// Status colors remain conceptually the same but refined
extension StatusColors on ColorScheme {
  Color get warning =>
      brightness == Brightness.light ? const Color(0xFFD97706) : const Color(0xFFF59E0B);

  Color get warningSurface =>
      brightness == Brightness.light ? const Color(0xFFFEF3C7) : const Color(0xFF451A03);

  Color get muted => onSurfaceVariant;

  Color get mutedSurface => surfaceContainerHighest;
}
