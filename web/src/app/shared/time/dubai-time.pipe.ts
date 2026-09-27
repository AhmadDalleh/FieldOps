import { Pipe, PipeTransform } from '@angular/core';
import { formatDubai } from './dubai-time';

@Pipe({ name: 'dubaiTime' })
export class DubaiTimePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return value ? formatDubai(value) : '';
  }
}
