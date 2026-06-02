import { Pipe, PipeTransform } from '@angular/core';
import { formatAvoidFour } from '../util/avoid-four';

@Pipe({ name: 'avoidFour', standalone: true })
export class AvoidFourPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return formatAvoidFour(value);
  }
}
