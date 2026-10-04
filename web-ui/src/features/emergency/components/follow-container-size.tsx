import { useEffect } from 'react';
import { useMap } from 'react-leaflet';

/** Leaflet only measures its box once, so a sidebar or pane resize leaves grey tiles until it is told to measure again. */
export function FollowContainerSize() {
  const map = useMap();
  useEffect(() => {
    const observer = new ResizeObserver(() => map.invalidateSize());
    observer.observe(map.getContainer());
    return () => observer.disconnect();
  }, [map]);
  return null;
}
