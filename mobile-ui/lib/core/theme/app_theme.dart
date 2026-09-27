import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

// Three layers that must stay visibly apart in both modes: the page (scaffold), the cards on it
// (scheme.surface), and the navigation bar. When they share one colour the screen reads as a flat sheet.
class _Layers {
  const _Layers({required this.page, required this.bar});

  final Color page;
  final Color bar;
}

class AppTheme {
  const AppTheme._();

  static const fontFamily = 'Figtree';

  static ThemeData get light => _build(_lightScheme, _lightLayers, BrandSurfaces.light);
  static ThemeData get dark => _build(_darkScheme, _darkLayers, BrandSurfaces.dark);

  static const _lightLayers = _Layers(page: Color(0xFFF4F7FA), bar: Color(0xFFFFFFFF));
  static const _darkLayers = _Layers(page: Color(0xFF0A0F1C), bar: Color(0xFF0F1526));

  static final _lightScheme = ColorScheme.fromSeed(
    seedColor: const Color(0xFF0F7C70),
    brightness: Brightness.light,
  ).copyWith(
    primary: const Color(0xFF0F7C70),
    onPrimary: const Color(0xFFFFFFFF),
    primaryContainer: const Color(0xFFDDF5EE),
    onPrimaryContainer: const Color(0xFF0B4F47),
    secondary: const Color(0xFF475467),
    onSecondary: const Color(0xFFFFFFFF),
    secondaryContainer: const Color(0xFFEEF2F6),
    onSecondaryContainer: const Color(0xFF1D2939),
    tertiary: const Color(0xFF5B4FD6),
    onTertiary: const Color(0xFFFFFFFF),
    tertiaryContainer: const Color(0xFFE9E6FD),
    onTertiaryContainer: const Color(0xFF2C2380),
    error: const Color(0xFFD93A3F),
    onError: const Color(0xFFFFFFFF),
    errorContainer: const Color(0xFFFFEDEC),
    onErrorContainer: const Color(0xFF7A1F22),
    surface: const Color(0xFFFFFFFF),
    onSurface: const Color(0xFF0F1B2D),
    onSurfaceVariant: const Color(0xFF5F6D83),
    surfaceContainerLowest: const Color(0xFFFFFFFF),
    surfaceContainerLow: const Color(0xFFF7F9FC),
    surfaceContainer: const Color(0xFFF1F4F8),
    surfaceContainerHigh: const Color(0xFFEAEEF3),
    surfaceContainerHighest: const Color(0xFFE4E9EF),
    outline: const Color(0xFF98A3B5),
    outlineVariant: const Color(0xFFE8EDF3),
    inverseSurface: const Color(0xFF1B2536),
    onInverseSurface: const Color(0xFFF1F4F8),
    inversePrimary: const Color(0xFF6FE0CB),
    shadow: const Color(0xFF101828),
  );

  static final _darkScheme = ColorScheme.fromSeed(
    seedColor: const Color(0xFF0F7C70),
    brightness: Brightness.dark,
  ).copyWith(
    primary: const Color(0xFF3FE0C8),
    onPrimary: const Color(0xFF00352E),
    primaryContainer: const Color(0xFF123A3E),
    onPrimaryContainer: const Color(0xFF7FF0DD),
    secondary: const Color(0xFFB4BFD3),
    onSecondary: const Color(0xFF1B2536),
    secondaryContainer: const Color(0xFF1C2538),
    onSecondaryContainer: const Color(0xFFDCE3F0),
    tertiary: const Color(0xFF9B8CFF),
    onTertiary: const Color(0xFF221A66),
    tertiaryContainer: const Color(0xFF221F4A),
    onTertiaryContainer: const Color(0xFFD9D3FF),
    error: const Color(0xFFFF5A5F),
    onError: const Color(0xFF45070B),
    errorContainer: const Color(0xFF2A1420),
    onErrorContainer: const Color(0xFFFFC9CB),
    surface: const Color(0xFF131A2A),
    onSurface: const Color(0xFFF1F4FA),
    onSurfaceVariant: const Color(0xFF93A0B8),
    surfaceContainerLowest: const Color(0xFF070B15),
    surfaceContainerLow: const Color(0xFF0F1524),
    surfaceContainer: const Color(0xFF172033),
    surfaceContainerHigh: const Color(0xFF1C263B),
    surfaceContainerHighest: const Color(0xFF243048),
    outline: const Color(0xFF65728C),
    outlineVariant: const Color(0xFF1F2940),
    inverseSurface: const Color(0xFFF1F4FA),
    onInverseSurface: const Color(0xFF131A2A),
    inversePrimary: const Color(0xFF0F7C70),
    shadow: const Color(0xFF000000),
  );

  static ThemeData _build(ColorScheme scheme, _Layers layers, BrandSurfaces brand) {
    final isLight = scheme.brightness == Brightness.light;
    final text = _text(
      ThemeData(colorScheme: scheme, useMaterial3: true, fontFamily: fontFamily).textTheme,
    ).apply(bodyColor: scheme.onSurface, displayColor: scheme.onSurface);
    final fieldBorder = isLight ? const Color(0xFFD6DDE6) : const Color(0xFF2C3852);

    return ThemeData(
      useMaterial3: true,
      fontFamily: fontFamily,
      colorScheme: scheme,
      textTheme: text,
      extensions: [brand],
      scaffoldBackgroundColor: layers.page,
      canvasColor: layers.page,
      dividerColor: scheme.outlineVariant,
      // The M3 default "sparkle" ripple compiles a shader on first touch, which stutters on
      // mid-range phones; the plain ripple is cheaper and looks the same at this size.
      splashFactory: InkRipple.splashFactory,
      appBarTheme: AppBarTheme(
        backgroundColor: WidgetStateColor.resolveWith(
          (states) => states.contains(WidgetState.scrolledUnder) ? layers.bar : layers.page,
        ),
        foregroundColor: scheme.onSurface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        toolbarHeight: 64,
        titleSpacing: gutter,
        titleTextStyle: text.titleLarge?.copyWith(fontSize: 22, fontWeight: FontWeight.w700),
        systemOverlayStyle: overlayStyleFor(scheme.brightness),
      ),
      // Light cards float on a soft shadow; dark ones are told apart by a lighter surface and a
      // hairline, because a shadow disappears against a near-black page.
      cardTheme: CardThemeData(
        elevation: isLight ? 1 : 0,
        shadowColor: isLight ? const Color(0x14101828) : Colors.transparent,
        margin: EdgeInsets.zero,
        color: scheme.surface,
        surfaceTintColor: Colors.transparent,
        clipBehavior: Clip.antiAlias,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(radiusL),
          side: isLight ? BorderSide.none : BorderSide(color: scheme.outlineVariant),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: isLight ? scheme.surface : scheme.surfaceContainerLow,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        labelStyle: text.bodyLarge?.copyWith(color: scheme.onSurfaceVariant),
        floatingLabelStyle: WidgetStateTextStyle.resolveWith(
          (states) => text.bodyMedium!.copyWith(
            fontWeight: FontWeight.w600,
            color: states.contains(WidgetState.error)
                ? scheme.error
                : states.contains(WidgetState.focused)
                ? scheme.primary
                : scheme.onSurfaceVariant,
          ),
        ),
        hintStyle: text.bodyLarge?.copyWith(color: scheme.onSurfaceVariant),
        helperStyle: text.bodySmall?.copyWith(color: scheme.onSurfaceVariant),
        prefixIconColor: scheme.onSurfaceVariant,
        suffixIconColor: scheme.onSurfaceVariant,
        border: _inputBorder(fieldBorder),
        enabledBorder: _inputBorder(fieldBorder),
        disabledBorder: _inputBorder(scheme.outlineVariant),
        focusedBorder: _inputBorder(scheme.primary, width: 2),
        errorBorder: _inputBorder(scheme.error),
        focusedErrorBorder: _inputBorder(scheme.error, width: 2),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          padding: const EdgeInsets.symmetric(horizontal: 22),
          elevation: 0,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusM)),
          textStyle: text.labelLarge?.copyWith(fontSize: 16),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          padding: const EdgeInsets.symmetric(horizontal: 22),
          foregroundColor: scheme.onSurface,
          backgroundColor: isLight ? scheme.surface : Colors.transparent,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusM)),
          side: BorderSide(color: fieldBorder),
          textStyle: text.labelLarge?.copyWith(fontSize: 16),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          minimumSize: const Size(48, 44),
          foregroundColor: scheme.primary,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusM)),
          textStyle: text.labelLarge,
        ),
      ),
      iconButtonTheme: IconButtonThemeData(
        style: IconButton.styleFrom(minimumSize: const Size(48, 48)),
      ),
      floatingActionButtonTheme: FloatingActionButtonThemeData(
        backgroundColor: scheme.primary,
        foregroundColor: scheme.onPrimary,
        elevation: 3,
        focusElevation: 3,
        hoverElevation: 4,
        highlightElevation: 2,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusL)),
        extendedTextStyle: text.labelLarge?.copyWith(fontSize: 15),
      ),
      segmentedButtonTheme: SegmentedButtonThemeData(
        style: ButtonStyle(
          minimumSize: const WidgetStatePropertyAll(Size(0, 48)),
          side: WidgetStatePropertyAll(BorderSide(color: fieldBorder)),
          shape: WidgetStatePropertyAll(
            RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusM)),
          ),
          backgroundColor: WidgetStateColor.resolveWith(
            (states) =>
                states.contains(WidgetState.selected) ? scheme.primaryContainer : scheme.surface,
          ),
          foregroundColor: WidgetStateColor.resolveWith(
            (states) => states.contains(WidgetState.selected)
                ? scheme.onPrimaryContainer
                : scheme.onSurface,
          ),
          textStyle: WidgetStatePropertyAll(text.labelLarge),
        ),
      ),
      chipTheme: ChipThemeData(
        backgroundColor: scheme.surface,
        selectedColor: scheme.primaryContainer,
        disabledColor: scheme.surfaceContainerHigh,
        side: BorderSide(color: fieldBorder),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusS)),
        labelStyle: text.labelLarge?.copyWith(color: scheme.onSurface),
        secondaryLabelStyle: text.labelLarge?.copyWith(color: scheme.onPrimaryContainer),
        checkmarkColor: scheme.onPrimaryContainer,
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 6),
      ),
      navigationBarTheme: NavigationBarThemeData(
        height: 72,
        elevation: 0,
        backgroundColor: layers.bar,
        surfaceTintColor: Colors.transparent,
        shadowColor: Colors.transparent,
        indicatorColor: scheme.primaryContainer,
        indicatorShape: const StadiumBorder(),
        labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
        iconTheme: WidgetStateProperty.resolveWith(
          (states) => IconThemeData(
            size: 24,
            color: states.contains(WidgetState.selected)
                ? scheme.onPrimaryContainer
                : scheme.onSurfaceVariant,
          ),
        ),
        labelTextStyle: WidgetStateProperty.resolveWith(
          (states) => text.labelMedium?.copyWith(
            letterSpacing: 0,
            color: states.contains(WidgetState.selected)
                ? scheme.onSurface
                : scheme.onSurfaceVariant,
          ),
        ),
      ),
      listTileTheme: ListTileThemeData(
        contentPadding: const EdgeInsets.symmetric(horizontal: gutter, vertical: 2),
        minVerticalPadding: 10,
        iconColor: scheme.onSurfaceVariant,
        textColor: scheme.onSurface,
        titleTextStyle: text.bodyLarge?.copyWith(fontWeight: FontWeight.w600),
        subtitleTextStyle: text.bodyMedium?.copyWith(color: scheme.onSurfaceVariant),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusM)),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: scheme.surface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusXL)),
        titleTextStyle: text.titleLarge,
        contentTextStyle: text.bodyMedium?.copyWith(color: scheme.onSurfaceVariant),
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: scheme.surface,
        modalBackgroundColor: scheme.surface,
        surfaceTintColor: Colors.transparent,
        showDragHandle: true,
        dragHandleColor: scheme.outline.withValues(alpha: 0.5),
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: Radius.circular(radiusXL)),
        ),
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: scheme.inverseSurface,
        contentTextStyle: text.bodyMedium?.copyWith(color: scheme.onInverseSurface),
        actionTextColor: scheme.inversePrimary,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusM)),
      ),
      dividerTheme: DividerThemeData(color: scheme.outlineVariant, space: 1, thickness: 1),
      progressIndicatorTheme: ProgressIndicatorThemeData(
        color: scheme.primary,
        linearTrackColor: scheme.surfaceContainerHighest,
        circularTrackColor: Colors.transparent,
      ),
      tooltipTheme: TooltipThemeData(
        decoration: BoxDecoration(
          color: scheme.inverseSurface,
          borderRadius: BorderRadius.circular(radiusS),
        ),
        textStyle: text.bodySmall?.copyWith(color: scheme.onInverseSurface),
      ),
      datePickerTheme: DatePickerThemeData(
        backgroundColor: scheme.surface,
        surfaceTintColor: Colors.transparent,
        headerBackgroundColor: brand.heroFrom,
        headerForegroundColor: brand.onHero,
        headerHeadlineStyle: text.headlineSmall,
        weekdayStyle: text.labelMedium?.copyWith(color: scheme.onSurfaceVariant),
        dayStyle: text.bodyMedium?.copyWith(fontWeight: FontWeight.w500),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusXL)),
      ),
      timePickerTheme: TimePickerThemeData(
        backgroundColor: scheme.surface,
        dialBackgroundColor: scheme.surfaceContainerHigh,
        hourMinuteColor: WidgetStateColor.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? scheme.primaryContainer
              : scheme.surfaceContainerHigh,
        ),
        hourMinuteTextColor: WidgetStateColor.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? scheme.onPrimaryContainer
              : scheme.onSurface,
        ),
        dayPeriodColor: scheme.primaryContainer,
        dayPeriodTextColor: WidgetStateColor.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? scheme.onPrimaryContainer
              : scheme.onSurfaceVariant,
        ),
        dayPeriodBorderSide: BorderSide(color: fieldBorder),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusXL)),
      ),
      popupMenuTheme: PopupMenuThemeData(
        color: scheme.surface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(radiusM),
          side: BorderSide(color: scheme.outlineVariant),
        ),
      ),
      dropdownMenuTheme: DropdownMenuThemeData(
        menuStyle: MenuStyle(
          backgroundColor: WidgetStatePropertyAll(scheme.surface),
          surfaceTintColor: const WidgetStatePropertyAll(Colors.transparent),
          shape: WidgetStatePropertyAll(
            RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(radiusM),
              side: BorderSide(color: scheme.outlineVariant),
            ),
          ),
        ),
      ),
    );
  }

  static SystemUiOverlayStyle overlayStyleFor(Brightness brightness) =>
      brightness == Brightness.light
      ? SystemUiOverlayStyle.dark.copyWith(statusBarColor: Colors.transparent)
      : SystemUiOverlayStyle.light.copyWith(statusBarColor: Colors.transparent);

  static TextTheme _text(TextTheme base) => base.copyWith(
    displaySmall: base.displaySmall?.copyWith(fontWeight: FontWeight.w700, letterSpacing: -0.6),
    headlineMedium: base.headlineMedium?.copyWith(
      fontSize: 28,
      fontWeight: FontWeight.w700,
      height: 1.2,
      letterSpacing: -0.5,
    ),
    headlineSmall: base.headlineSmall?.copyWith(
      fontSize: 23,
      fontWeight: FontWeight.w700,
      height: 1.25,
      letterSpacing: -0.3,
    ),
    titleLarge: base.titleLarge?.copyWith(
      fontSize: 20,
      fontWeight: FontWeight.w700,
      height: 1.3,
      letterSpacing: -0.2,
    ),
    titleMedium: base.titleMedium?.copyWith(fontSize: 16.5, fontWeight: FontWeight.w700),
    titleSmall: base.titleSmall?.copyWith(fontSize: 15, fontWeight: FontWeight.w600),
    bodyLarge: base.bodyLarge?.copyWith(fontSize: 16, height: 1.5, letterSpacing: 0),
    bodyMedium: base.bodyMedium?.copyWith(fontSize: 14.5, height: 1.45, letterSpacing: 0),
    bodySmall: base.bodySmall?.copyWith(fontSize: 13, height: 1.4, letterSpacing: 0),
    labelLarge: base.labelLarge?.copyWith(
      fontSize: 14.5,
      fontWeight: FontWeight.w600,
      letterSpacing: 0,
    ),
    labelMedium: base.labelMedium?.copyWith(
      fontSize: 12.5,
      fontWeight: FontWeight.w600,
      letterSpacing: 0,
    ),
    labelSmall: base.labelSmall?.copyWith(fontWeight: FontWeight.w600, letterSpacing: 0.2),
  );

  static OutlineInputBorder _inputBorder(Color color, {double width = 1}) => OutlineInputBorder(
    borderRadius: BorderRadius.circular(radiusM),
    borderSide: BorderSide(color: color, width: width),
  );

  static const radiusS = 10.0;
  static const radiusM = 14.0;
  static const radiusL = 20.0;
  static const radiusXL = 28.0;

  static const gutter = 20.0;
}

// Colours outside the Material roles: the home "what's happening now" card (a gradient, in a
// different mood per mode), the soft wash behind the light home header, and the four pastel
// shortcut tiles. Kept here so a screen never hard-codes them.
@immutable
class BrandSurfaces extends ThemeExtension<BrandSurfaces> {
  const BrandSurfaces({
    required this.heroFrom,
    required this.heroTo,
    required this.onHero,
    required this.onHeroMuted,
    required this.heroTrack,
    required this.heroChip,
    required this.accents,
    this.heroGlow,
    this.pageWash,
  });

  final Color heroFrom;
  final Color heroTo;
  final Color onHero;
  final Color onHeroMuted;
  final Color heroTrack;
  final Color heroChip;
  final Color? heroGlow;
  final Color? pageWash;

  // (background, foreground) pairs for shortcut tiles. Light: teal, violet, amber, rose.
  final List<(Color, Color)> accents;

  static const light = BrandSurfaces(
    heroFrom: Color(0xFFD9F6EE),
    heroTo: Color(0xFFD6ECFB),
    onHero: Color(0xFF0B3B35),
    onHeroMuted: Color(0xFF3E6A64),
    heroTrack: Color(0x290B3B35),
    heroChip: Color(0xB3FFFFFF),
    pageWash: Color(0xFFE3F6F1),
    accents: [
      (Color(0xFFDDF5EE), Color(0xFF0F7C70)),
      (Color(0xFFE9E6FD), Color(0xFF5B4FD6)),
      (Color(0xFFFFEEDD), Color(0xFFB85A08)),
      (Color(0xFFFFE6EA), Color(0xFFC9304F)),
    ],
  );

  // Dark stays muted on purpose: a saturated gradient and four neon tiles read as a consumer
  // app on a dark background, so every tile shares one quiet teal.
  static const dark = BrandSurfaces(
    heroFrom: Color(0xFF15343A),
    heroTo: Color(0xFF16263A),
    onHero: Color(0xFFF1F4FA),
    onHeroMuted: Color(0xFFA9BAC8),
    heroTrack: Color(0x2EFFFFFF),
    heroChip: Color(0x1FFFFFFF),
    accents: [
      (Color(0xFF172234), Color(0xFF7CCFC2)),
      (Color(0xFF172234), Color(0xFF7CCFC2)),
      (Color(0xFF172234), Color(0xFF7CCFC2)),
      (Color(0xFF172234), Color(0xFF7CCFC2)),
    ],
  );

  static BrandSurfaces of(BuildContext context) => Theme.of(context).extension<BrandSurfaces>()!;

  @override
  BrandSurfaces copyWith() => this;

  // Only ever swapped whole on a light/dark change, so no in-between colours are needed.
  @override
  BrandSurfaces lerp(BrandSurfaces? other, double t) => t < 0.5 || other == null ? this : other;
}

// Deliberately three signals only: warning (amber), error (scheme red) and muted (grey) — a blue "info" accent was tried and dropped as indistinguishable from the brand teal.
extension StatusColors on ColorScheme {
  Color get warning =>
      brightness == Brightness.light ? const Color(0xFF9A4A06) : const Color(0xFFF5B85C);

  Color get warningSurface =>
      brightness == Brightness.light ? const Color(0xFFFEF3E2) : const Color(0xFF3A2A12);

  Color get muted => onSurfaceVariant;

  Color get mutedSurface => surfaceContainerHighest;
}
