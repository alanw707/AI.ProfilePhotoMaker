import { CommonModule } from '@angular/common';
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

  private readonly _meta = inject(Meta);
  private readonly _title = inject(Title);

  ngOnInit(): void {
    this._title.setTitle('Blog - AI Profile Photo Maker');
    this._meta.updateTag({
      name: 'description',
      content:
        'Practical guides and tips for better AI headshots, LinkedIn photos, and professional profile pictures.',
    });
  }
}
