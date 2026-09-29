import { fitWithin, jpegName } from './image-compress';

describe('photo compression', () => {
  it('scales the longer side down to 1600px', () => {
    expect(fitWithin(4000, 3000)).toEqual({ width: 1600, height: 1200 });
    expect(fitWithin(3000, 4000)).toEqual({ width: 1200, height: 1600 });
  });

  it('never enlarges a small photo', () => {
    expect(fitWithin(800, 600)).toEqual({ width: 800, height: 600 });
  });

  it('renames to .jpg', () => {
    expect(jpegName('IMG_0001.HEIC')).toBe('IMG_0001.jpg');
    expect(jpegName('before')).toBe('before.jpg');
    expect(jpegName('.png')).toBe('photo.jpg');
  });
});
