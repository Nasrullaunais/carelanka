import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

/// The first screen on every launch: the CareLanka mark settles in while the stored session is
/// checked. [onFinished] fires once the animation has played, and the router holds here until then.
class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key, required this.onFinished});

  final VoidCallback onFinished;

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen> with SingleTickerProviderStateMixin {
  static const _duration = Duration(milliseconds: 1800);
  static const _reducedMotionDuration = Duration(milliseconds: 400);

  late final AnimationController _controller = AnimationController(vsync: this)
    ..addStatusListener(_onStatus);

  bool _started = false;
  bool _finished = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_started) return;
    _started = true;
    _controller.duration =
        MediaQuery.disableAnimationsOf(context) ? _reducedMotionDuration : _duration;
    _controller.forward();
  }

  void _onStatus(AnimationStatus status) {
    if (status != AnimationStatus.completed) return;
    setState(() => _finished = true);
    widget.onFinished();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Animation<double> _interval(double begin, double end, [Curve curve = Curves.easeOutCubic]) =>
      CurvedAnimation(parent: _controller, curve: Interval(begin, end, curve: curve));

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    final logoFade = _interval(0.0, 0.4, Curves.easeOut);
    final logoScale =
        Tween(begin: 0.86, end: 1.0).animate(_interval(0.0, 0.6, Curves.easeOutQuart));
    final ring = _interval(0.1, 0.83, Curves.easeOutQuart);
    final nameFade = _interval(0.35, 0.83, Curves.easeOut);
    final nameSlide = Tween(begin: const Offset(0, 0.25), end: Offset.zero)
        .animate(_interval(0.35, 0.83, Curves.easeOutQuart));

    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            SizedBox(
              width: 160,
              height: 160,
              child: Stack(
                alignment: Alignment.center,
                children: [
                  AnimatedBuilder(
                    animation: ring,
                    builder: (_, __) => Opacity(
                      opacity: math.sin(ring.value * math.pi) * 0.4,
                      child: Transform.scale(
                        scale: 0.6 + ring.value * 0.65,
                        child: Container(
                          width: 120,
                          height: 120,
                          decoration: BoxDecoration(
                            shape: BoxShape.circle,
                            color: scheme.primary.withValues(alpha: 0.35),
                          ),
                        ),
                      ),
                    ),
                  ),
                  FadeTransition(
                    opacity: logoFade,
                    child: ScaleTransition(
                      scale: logoScale,
                      child: Container(
                        width: 96,
                        height: 96,
                        decoration: BoxDecoration(
                          color: scheme.primaryContainer,
                          borderRadius: BorderRadius.circular(AppTheme.radiusXL),
                        ),
                        child: Icon(
                          Icons.local_hospital_rounded,
                          size: 52,
                          color: scheme.onPrimaryContainer,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 8),
            FadeTransition(
              opacity: nameFade,
              child: SlideTransition(
                position: nameSlide,
                child: Text(
                  'CareLanka',
                  style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w700),
                ),
              ),
            ),
            const SizedBox(height: 32),
            // Only seen when the session check outlasts the animation, e.g. on a slow network.
            SizedBox(
              height: 24,
              child: AnimatedOpacity(
                opacity: _finished ? 1 : 0,
                duration: const Duration(milliseconds: 300),
                child: _finished
                    ? const SizedBox.square(
                        dimension: 22,
                        child: CircularProgressIndicator(strokeWidth: 2.5),
                      )
                    : null,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
