import { GalleryComponent } from './gallery.component';
import { GalleryImage } from '../../components/photo-gallery/photo-gallery.component';

describe('GalleryComponent Studio handoff', () => {
  it('routes the owned Gallery image ID to Studio refinement', () => {
    const router = jasmine.createSpyObj('Router', ['navigate']);
    const component = new GalleryComponent(
      {} as any,
      router,
      {} as any,
      {} as any,
      {} as any,
      {} as any,
      {} as any,
      {} as any
    );
    const image: GalleryImage = {
      id: 42,
      url: 'gallery-image-url',
      title: 'Generated Photo',
      createdAt: new Date(),
      status: 'completed',
      type: 'generated',
    };

    component.onImageRefine(image);

    expect(router.navigate).toHaveBeenCalledOnceWith(['/app/enhance'], {
      queryParams: { refineImageId: image.id },
    });
  });
});
