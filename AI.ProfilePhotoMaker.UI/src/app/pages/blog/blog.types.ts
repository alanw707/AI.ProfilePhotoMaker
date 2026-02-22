export interface BlogPost {
  slug: string;
  title: string;
  description: string;
  dateIso: string;
  author: string;
  tags: string[];
  contentHtml: string;
}
