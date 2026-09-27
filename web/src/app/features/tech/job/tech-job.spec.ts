import { WorkOrder } from '../../work-orders/work-orders.api';
import { directionsUrl, formatMinutes } from './tech-job';

const site = (lat: number | null, lng: number | null) =>
  ({ addressLine1: 'Sheikh Zayed Road', addressLine2: 'Tower 2', city: 'Dubai', latitude: lat, longitude: lng }) as WorkOrder['site'];

describe('technician job helpers', () => {
  it('navigates to the pin when the site has one, otherwise to the address', () => {
    expect(directionsUrl(site(25.2, 55.27))).toBe('https://www.google.com/maps/dir/?api=1&destination=25.2%2C55.27');
    expect(directionsUrl(site(null, null))).toBe(
      'https://www.google.com/maps/dir/?api=1&destination=Sheikh%20Zayed%20Road%2C%20Tower%202%2C%20Dubai',
    );
  });

  it('formats durations', () => {
    expect(formatMinutes(null)).toBe('running');
    expect(formatMinutes(45)).toBe('45 min');
    expect(formatMinutes(65)).toBe('1 h 05 min');
  });
});
