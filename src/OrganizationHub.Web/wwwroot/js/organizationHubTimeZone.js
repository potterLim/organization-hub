export function getClientTimeZoneId() {
    const resolvedOptions = Intl.DateTimeFormat().resolvedOptions();
    return resolvedOptions.timeZone || "";
}
