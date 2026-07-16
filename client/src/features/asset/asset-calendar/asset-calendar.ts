import { Component, Input, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AssetService } from '../../../core/services/asset-service';
import { AssetContractPeriodDto } from '../../../types/asset';

interface CalendarDay {
  date: Date | null;
  contracts: AssetContractPeriodDto[];
}

@Component({
  selector: 'app-asset-calendar',
  imports: [RouterLink],
  templateUrl: './asset-calendar.html',
})
export class AssetCalendar {
  @Input({ required: true }) assetId!: string;

  private service = inject(AssetService);

  isOpen = signal(false);
  loading = signal(false);
  periods = signal<AssetContractPeriodDto[]>([]);

  private todayDate = (() => {
    const d = new Date();
    return new Date(d.getFullYear(), d.getMonth(), d.getDate());
  })();

  currentYear = signal(new Date().getFullYear());
  currentMonth = signal(new Date().getMonth());

  readonly DAY_NAMES = ['Δε', 'Τρ', 'Τε', 'Πε', 'Πα', 'Σά', 'Κυ'];

  monthLabel = computed(() =>
    new Date(this.currentYear(), this.currentMonth(), 1)
      .toLocaleDateString('el-GR', { month: 'long', year: 'numeric' })
  );

  calendarDays = computed((): CalendarDay[] => {
    const year = this.currentYear();
    const month = this.currentMonth();
    const firstDay = new Date(year, month, 1);
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const startPad = (firstDay.getDay() + 6) % 7; // Mon=0 … Sun=6

    const days: CalendarDay[] = Array.from({ length: startPad }, () => ({ date: null, contracts: [] }));

    for (let d = 1; d <= daysInMonth; d++) {
      const date = new Date(year, month, d);
      const contracts = this.periods().filter(p => {
        const s = new Date(p.startDate);
        const e = new Date(p.endDate);
        const start = new Date(s.getFullYear(), s.getMonth(), s.getDate());
        const end = new Date(e.getFullYear(), e.getMonth(), e.getDate());
        return date >= start && date <= end;
      });
      days.push({ date, contracts });
    }

    return days;
  });

  toggle() {
    this.isOpen.update(v => !v);
    if (this.isOpen() && this.periods().length === 0) this.load();
  }

  load() {
    this.loading.set(true);
    this.service.getContractPeriods(this.assetId).subscribe({
      next: periods => {
        this.periods.set(periods);
        const relevant = periods.find(p => new Date(p.endDate) >= this.todayDate);
        if (relevant) {
          const s = new Date(relevant.startDate);
          this.currentYear.set(s.getFullYear());
          this.currentMonth.set(s.getMonth());
        }
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); }
    });
  }

  prevMonth() {
    if (this.currentMonth() === 0) { this.currentMonth.set(11); this.currentYear.update(y => y - 1); }
    else this.currentMonth.update(m => m - 1);
  }

  nextMonth() {
    if (this.currentMonth() === 11) { this.currentMonth.set(0); this.currentYear.update(y => y + 1); }
    else this.currentMonth.update(m => m + 1);
  }

  isToday(date: Date): boolean {
    return date.getTime() === this.todayDate.getTime();
  }

  dayBgClass(day: CalendarDay): string {
    if (!day.contracts.length || !day.date) return '';
    const d = day.date;
    if (d.getTime() === this.todayDate.getTime())
      return 'bg-success text-success-content font-bold';
    if (d < this.todayDate)
      return 'bg-base-300 text-base-content/50 line-through';
    return 'bg-error/20 text-error font-semibold border border-error/30';
  }

  getTooltip(day: CalendarDay): string {
    return day.contracts.map(c => {
      const s = new Date(c.startDate).toLocaleDateString('el-GR', { day: '2-digit', month: '2-digit', year: 'numeric' });
      const e = new Date(c.endDate).toLocaleDateString('el-GR', { day: '2-digit', month: '2-digit', year: 'numeric' });
      return `${c.customerName}: ${s} – ${e}`;
    }).join(' | ');
  }
}