a4e576e	2024-09-18T16:18:37-07:00

Address PR Feedback

- Lesser becomes ComparableValue
- Move ComparableValue to evalers only
- Move all magic values to constants
- TypeError when incompatible comparable types
- Support more deserialization for duration/datetime
- Make the datetime parser easier to follow
- Drop UnsafeDatetime in favor of FromStdTime(time.UnixMilli(..))
- Document methods
- Test Coverage to 100%

Signed-off-by: Andrew Gwozdziewycz <andrew.gwozdziewycz@strongdm.com>
