import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'patient_shots_test.dart' show loadRealFonts, phone;

// Throwaway direction mock-ups of the patient Home, for choosing a look. Not app code.

class _Palette {
  const _Palette({
    required this.page,
    required this.card,
    required this.text,
    required this.muted,
    required this.line,
    required this.heroFrom,
    required this.heroTo,
    required this.heroText,
    required this.heroMuted,
    required this.sosSurface,
    required this.sosIcon,
    required this.sosText,
    required this.tiles,
    required this.bar,
    required this.pill,
    required this.pillIcon,
    required this.dark,
    this.wash,
    this.heroGlow,
  });

  final Color page, card, text, muted, line;
  final Color heroFrom, heroTo, heroText, heroMuted;
  final Color sosSurface, sosIcon, sosText;
  final List<(Color, Color)> tiles;
  final Color bar, pill, pillIcon;
  final bool dark;
  final Color? wash, heroGlow;
}

const _airy = _Palette(
  page: Color(0xFFF4F7FA),
  card: Color(0xFFFFFFFF),
  text: Color(0xFF0F1B2D),
  muted: Color(0xFF66748A),
  line: Color(0xFFE8EDF3),
  heroFrom: Color(0xFFD9F6EE),
  heroTo: Color(0xFFD6ECFB),
  heroText: Color(0xFF0B3B35),
  heroMuted: Color(0xFF3E6A64),
  sosSurface: Color(0xFFFFEDEC),
  sosIcon: Color(0xFFE5484D),
  sosText: Color(0xFF7A1F22),
  tiles: [
    (Color(0xFFDDF5EE), Color(0xFF0F7C70)),
    (Color(0xFFE9E6FD), Color(0xFF5B4FD6)),
    (Color(0xFFFFEEDD), Color(0xFFC2620A)),
    (Color(0xFFFFE6EA), Color(0xFFD13458)),
  ],
  bar: Color(0xFFFFFFFF),
  pill: Color(0xFFDDF5EE),
  pillIcon: Color(0xFF0B5C53),
  dark: false,
  wash: Color(0xFFE3F6F1),
);

const _rich = _Palette(
  page: Color(0xFF0A0F1C),
  card: Color(0xFF131A2A),
  text: Color(0xFFF1F4FA),
  muted: Color(0xFF93A0B8),
  line: Color(0xFF1F2940),
  heroFrom: Color(0xFF0FA697),
  heroTo: Color(0xFF2F6FE0),
  heroText: Color(0xFFFFFFFF),
  heroMuted: Color(0xFFD6ECF5),
  sosSurface: Color(0xFF2A1420),
  sosIcon: Color(0xFFFF5A5F),
  sosText: Color(0xFFFFC9CB),
  tiles: [
    (Color(0xFF0E3A3A), Color(0xFF3FE0C8)),
    (Color(0xFF221F4A), Color(0xFF9B8CFF)),
    (Color(0xFF3A2A12), Color(0xFFFFB45C)),
    (Color(0xFF3A1622), Color(0xFFFF6B8B)),
  ],
  bar: Color(0xFF0F1526),
  pill: Color(0xFF16323A),
  pillIcon: Color(0xFF5EEAD4),
  dark: true,
  heroGlow: Color(0x552F6FE0),
);

class _Home extends StatelessWidget {
  const _Home(this.p);

  final _Palette p;

  @override
  Widget build(BuildContext context) {
    TextStyle t(double size, FontWeight w, Color c, {double h = 1.3, double ls = 0}) =>
        TextStyle(fontFamily: 'Figtree', fontSize: size, fontWeight: w, color: c, height: h, letterSpacing: ls);

    Widget shadowed({required Widget child, required BorderRadius r, Color? color, Gradient? gradient}) {
      return Container(
        decoration: BoxDecoration(
          color: color,
          gradient: gradient,
          borderRadius: r,
          border: p.dark ? Border.all(color: p.line) : null,
          boxShadow: p.dark
              ? null
              : const [
                  BoxShadow(color: Color(0x0F101828), blurRadius: 16, offset: Offset(0, 6)),
                  BoxShadow(color: Color(0x0A101828), blurRadius: 3, offset: Offset(0, 1)),
                ],
        ),
        child: child,
      );
    }

    final steps = ['Waiting for a bed', 'Bed ready', 'In hospital'];

    final hero = Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(26),
        gradient: LinearGradient(
          colors: [p.heroFrom, p.heroTo],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        boxShadow: [
          if (p.heroGlow != null) BoxShadow(color: p.heroGlow!, blurRadius: 30, offset: const Offset(0, 12)),
          if (!p.dark) const BoxShadow(color: Color(0x14101828), blurRadius: 18, offset: Offset(0, 8)),
        ],
      ),
      padding: const EdgeInsets.fromLTRB(20, 18, 20, 18),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                decoration: BoxDecoration(
                  color: p.dark ? const Color(0x33FFFFFF) : const Color(0xB3FFFFFF),
                  borderRadius: BorderRadius.circular(99),
                ),
                child: Row(
                  children: [
                    Icon(Icons.bed_outlined, size: 15, color: p.heroText),
                    const SizedBox(width: 6),
                    Text('Your stay', style: t(12.5, FontWeight.w700, p.heroText)),
                  ],
                ),
              ),
              const Spacer(),
              Icon(Icons.arrow_forward_rounded, color: p.heroText),
            ],
          ),
          const SizedBox(height: 14),
          Text('A bed is being\narranged for you.', style: t(25, FontWeight.w700, p.heroText, h: 1.2, ls: -0.4)),
          const SizedBox(height: 16),
          Row(
            children: [
              for (var i = 0; i < steps.length; i++) ...[
                Expanded(
                  child: Container(
                    height: 6,
                    decoration: BoxDecoration(
                      color: i == 0
                          ? p.heroText
                          : (p.dark ? const Color(0x40FFFFFF) : p.heroText.withValues(alpha: 0.16)),
                      borderRadius: BorderRadius.circular(9),
                    ),
                  ),
                ),
                if (i < steps.length - 1) const SizedBox(width: 6),
              ],
            ],
          ),
          const SizedBox(height: 8),
          Text('Step 1 of 3 · Reception is finding you a bed', style: t(13, FontWeight.w500, p.heroMuted)),
        ],
      ),
    );

    final tiles = [
      (Icons.calendar_month_rounded, 'Book a\nvisit'),
      (Icons.description_outlined, 'My\nreports'),
      (Icons.medication_outlined, 'Prescrip-\ntions'),
      (Icons.history_rounded, 'Past\nvisits'),
    ];

    return Scaffold(
      backgroundColor: p.page,
      body: Stack(
        children: [
          if (p.wash != null)
            Container(
              height: 260,
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  colors: [p.wash!, p.page],
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                ),
              ),
            ),
          SafeArea(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(20, 18, 20, 24),
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Good morning', style: t(15, FontWeight.w500, p.muted)),
                          Text('Kasun', style: t(30, FontWeight.w800, p.text, h: 1.15, ls: -0.6)),
                        ],
                      ),
                    ),
                    Container(
                      width: 48,
                      height: 48,
                      decoration: BoxDecoration(
                        shape: BoxShape.circle,
                        color: p.card,
                        border: Border.all(color: p.tiles[0].$2.withValues(alpha: 0.5), width: 2),
                      ),
                      alignment: Alignment.center,
                      child: Text('KM', style: t(16, FontWeight.w700, p.tiles[0].$2)),
                    ),
                  ],
                ),
                const SizedBox(height: 22),
                hero,
                const SizedBox(height: 22),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    for (var i = 0; i < tiles.length; i++)
                      Column(
                        children: [
                          Container(
                            width: 62,
                            height: 62,
                            decoration: BoxDecoration(
                              color: p.tiles[i].$1,
                              borderRadius: BorderRadius.circular(20),
                            ),
                            child: Icon(tiles[i].$1, color: p.tiles[i].$2, size: 27),
                          ),
                          const SizedBox(height: 8),
                          Text(tiles[i].$2, textAlign: TextAlign.center, style: t(12.5, FontWeight.w600, p.text, h: 1.2)),
                        ],
                      ),
                  ],
                ),
                const SizedBox(height: 24),
                shadowed(
                  color: p.sosSurface,
                  r: BorderRadius.circular(22),
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Row(
                      children: [
                        Container(
                          width: 46,
                          height: 46,
                          decoration: BoxDecoration(color: p.sosIcon, shape: BoxShape.circle),
                          child: const Icon(Icons.emergency_outlined, color: Colors.white),
                        ),
                        const SizedBox(width: 14),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('Request ambulance', style: t(16.5, FontWeight.w700, p.sosText)),
                              Text('Share your location', style: t(14, FontWeight.w500, p.sosText.withValues(alpha: 0.75))),
                            ],
                          ),
                        ),
                        Icon(Icons.chevron_right_rounded, color: p.sosText),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 26),
                Text('Emergency contact', style: t(17, FontWeight.w700, p.text)),
                const SizedBox(height: 12),
                shadowed(
                  color: p.card,
                  r: BorderRadius.circular(22),
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Row(
                      children: [
                        CircleAvatar(
                          radius: 23,
                          backgroundColor: p.tiles[1].$1,
                          child: Text('NM', style: t(15, FontWeight.w700, p.tiles[1].$2)),
                        ),
                        const SizedBox(width: 14),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('Nadeesha Mendis', style: t(16, FontWeight.w700, p.text)),
                              Text('+94 77 123 4502', style: t(14, FontWeight.w500, p.muted)),
                            ],
                          ),
                        ),
                        Container(
                          width: 44,
                          height: 44,
                          decoration: BoxDecoration(color: p.tiles[0].$1, shape: BoxShape.circle),
                          child: Icon(Icons.call_rounded, color: p.tiles[0].$2, size: 21),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
      bottomNavigationBar: Container(
        decoration: BoxDecoration(
          color: p.bar,
          border: Border(top: BorderSide(color: p.line)),
          boxShadow: p.dark ? null : const [BoxShadow(color: Color(0x0D101828), blurRadius: 20, offset: Offset(0, -4))],
        ),
        height: 78,
        child: Row(
          children: [
            for (final (icon, label, on) in [
              (Icons.home_rounded, 'Home', true),
              (Icons.calendar_month_outlined, '', false),
              (Icons.monitor_heart_outlined, '', false),
              (Icons.medication_outlined, '', false),
              (Icons.person_outline_rounded, '', false),
            ])
              Expanded(
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Container(
                      width: 60,
                      height: 32,
                      decoration: BoxDecoration(
                        color: on ? p.pill : Colors.transparent,
                        borderRadius: BorderRadius.circular(16),
                      ),
                      child: Icon(icon, color: on ? p.pillIcon : p.muted),
                    ),
                    if (on) ...[
                      const SizedBox(height: 4),
                      Text(label, style: t(12.5, FontWeight.w700, p.text)),
                    ],
                  ],
                ),
              ),
          ],
        ),
      ),
    );
  }
}

void main() {
  setUpAll(loadRealFonts);

  for (final (name, palette) in [('A_bright_airy', _airy), ('B_dark_rich', _rich)]) {
    testWidgets('home variant $name', (tester) async {
      tester.view.physicalSize = phone * 3;
      tester.view.devicePixelRatio = 3;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(
        MaterialApp(debugShowCheckedModeBanner: false, home: _Home(palette)),
      );
      await tester.pumpAndSettle();
      await expectLater(find.byType(MaterialApp), matchesGoldenFile('shots/variant_$name.png'));
    });
  }
}
