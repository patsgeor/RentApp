export type Feature = {
  icon: string;
  title: string;
  description: string;
}


export type Plan ={
  name: string;
  tagline: string;
  monthly: number;
  features: string[];
  highlighted: boolean;
  cta: string;
}