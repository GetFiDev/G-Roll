// GRollHaptics.mm
// iOS Native Haptic Feedback Implementation for G-Roll
// This file provides native iOS haptic feedback using UIKit's haptic generators.

#import <UIKit/UIKit.h>

// Impact Feedback Generator (for physical impacts)
static UIImpactFeedbackGenerator* _impactGenerators[3] = {nil, nil, nil};

// Notification Feedback Generator (for success/warning/error)
static UINotificationFeedbackGenerator* _notificationGenerator = nil;

// Selection Feedback Generator (for selection changes)
static UISelectionFeedbackGenerator* _selectionGenerator = nil;

extern "C" {

    // Initialize generators lazily
    void _EnsureImpactGenerator(int style) {
        if (_impactGenerators[style] == nil) {
            UIImpactFeedbackStyle feedbackStyle;
            switch (style) {
                case 0: feedbackStyle = UIImpactFeedbackStyleLight; break;
                case 1: feedbackStyle = UIImpactFeedbackStyleMedium; break;
                case 2: feedbackStyle = UIImpactFeedbackStyleHeavy; break;
                default: feedbackStyle = UIImpactFeedbackStyleMedium; break;
            }
            _impactGenerators[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:feedbackStyle];
            [_impactGenerators[style] prepare];
        }
    }

    void _EnsureNotificationGenerator() {
        if (_notificationGenerator == nil) {
            _notificationGenerator = [[UINotificationFeedbackGenerator alloc] init];
            [_notificationGenerator prepare];
        }
    }

    void _EnsureSelectionGenerator() {
        if (_selectionGenerator == nil) {
            _selectionGenerator = [[UISelectionFeedbackGenerator alloc] init];
            [_selectionGenerator prepare];
        }
    }

    // Trigger impact haptic feedback
    // style: 0 = Light, 1 = Medium, 2 = Heavy
    void _TriggerImpactHaptic(int style) {
        if (@available(iOS 10.0, *)) {
            if (style < 0 || style > 2) style = 1; // Default to medium
            _EnsureImpactGenerator(style);
            [_impactGenerators[style] impactOccurred];
        }
    }

    // Trigger notification haptic feedback
    // type: 0 = Success, 1 = Warning, 2 = Error
    void _TriggerNotificationHaptic(int type) {
        if (@available(iOS 10.0, *)) {
            _EnsureNotificationGenerator();
            UINotificationFeedbackType feedbackType;
            switch (type) {
                case 0: feedbackType = UINotificationFeedbackTypeSuccess; break;
                case 1: feedbackType = UINotificationFeedbackTypeWarning; break;
                case 2: feedbackType = UINotificationFeedbackTypeError; break;
                default: feedbackType = UINotificationFeedbackTypeSuccess; break;
            }
            [_notificationGenerator notificationOccurred:feedbackType];
        }
    }

    // Trigger selection haptic feedback (for picker, toggle, etc.)
    void _TriggerSelectionHaptic() {
        if (@available(iOS 10.0, *)) {
            _EnsureSelectionGenerator();
            [_selectionGenerator selectionChanged];
        }
    }

    // Prepare all generators (call on app launch for better responsiveness)
    void _PrepareHapticGenerators() {
        if (@available(iOS 10.0, *)) {
            for (int i = 0; i < 3; i++) {
                _EnsureImpactGenerator(i);
            }
            _EnsureNotificationGenerator();
            _EnsureSelectionGenerator();
        }
    }

    // Release all generators (optional, for memory cleanup)
    void _ReleaseHapticGenerators() {
        for (int i = 0; i < 3; i++) {
            _impactGenerators[i] = nil;
        }
        _notificationGenerator = nil;
        _selectionGenerator = nil;
    }
}
