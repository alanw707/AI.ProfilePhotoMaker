import { CommonModule, DOCUMENT } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterModule } from '@angular/router';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';
import { MarketingHeaderComponent } from '../../shared/marketing-header/marketing-header.component';
import { blogPosts } from './blog.data';

@Component({
  selector: 'app-blog-list',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingHeaderComponent, MarketingFooterComponent],
  templateUrl: './blog-list.component.html',
  styleUrls: ['./blog.sass'],
})
export class BlogListComponent implements OnInit {
  posts = blogPosts;
  menuPosts = blogPosts.slice(0, 4);

  private readonly _meta = inject(Meta);
  private readonly _title = inject(Title);
  private readonly _document = inject(DOCUMENT);

  ngOnInit(): void {
    const canonicalUrl = 'https://aiprofilephotomaker.com/blog';

    this._title.setTitle('Blog - AI Profile Photo Maker');
    this._meta.updateTag({
      name: 'description',
      content:
        'Practical guides and tips for better AI headshots, LinkedIn photos, and professional profile pictures.',
    });
    this._meta.updateTag({ name: 'robots', content: 'index, follow' });
    this._meta.updateTag({ property: 'og:title', content: 'Blog - AI Profile Photo Maker' });
    this._meta.updateTag({
      property: 'og:description',
      content:
        'Practical guides and tips for better AI headshots, LinkedIn photos, and professional profile pictures.',
    });
    this._meta.updateTag({ property: 'og:type', content: 'website' });
    this._meta.updateTag({ property: 'og:url', content: canonicalUrl });
    this._meta.updateTag({
      property: 'og:image',
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this._meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this._meta.updateTag({ name: 'twitter:title', content: 'Blog - AI Profile Photo Maker' });
    this._meta.updateTag({
      name: 'twitter:description',
      content:
        'Practical guides and tips for better AI headshots, LinkedIn photos, and professional profile pictures.',
    });
    this._meta.updateTag({
      name: 'twitter:image',
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this._meta.updateTag({ name: 'twitter:url', content: canonicalUrl });

    this.setCanonicalUrl(canonicalUrl);
  }

  private setCanonicalUrl(url: string): void {
    let canonical = this._document.querySelector("link[rel='canonical']") as HTMLLinkElement | null;
    if (!canonical) {
      canonical = this._document.createElement('link');
      canonical.setAttribute('rel', 'canonical');
      this._document.head.appendChild(canonical);
    }
    canonical.setAttribute('href', url);
  }
}
