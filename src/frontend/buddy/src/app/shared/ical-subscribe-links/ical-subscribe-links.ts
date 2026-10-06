import { Component, computed, input } from '@angular/core';

import { TranslatePipe } from '../../core/i18n/translate.pipe';

// Calendar apps poll a subscribed feed URL for updates; opening or importing the downloaded .ics
// file instead copies the events once and never updates them. These links hand the feed URL
// straight to a calendar app's subscribe dialog.
@Component({
  selector: 'app-ical-subscribe-links',
  imports: [TranslatePipe],
  templateUrl: './ical-subscribe-links.html',
})
export class IcalSubscribeLinks {
  readonly url = input.required<string>();

  // webcal:// is the scheme Apple Calendar and Outlook register to open a subscribe dialog.
  protected readonly webcalUrl = computed(() => this.url().replace(/^https?:/, 'webcal:'));
  // Google Calendar doesn't register for webcal:, but subscribes to a feed passed as `cid`.
  protected readonly googleUrl = computed(
    () => `https://calendar.google.com/calendar/r?cid=${encodeURIComponent(this.webcalUrl())}`,
  );
}
