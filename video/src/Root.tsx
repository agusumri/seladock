import { Composition } from "remotion";
import { SelaDockScene } from "./SelaDockScene";

const FPS = 30;
const DURATION_SECONDS = 14;

export const RemotionRoot: React.FC = () => {
  return (
    <Composition
      id="SelaDock"
      component={SelaDockScene}
      durationInFrames={FPS * DURATION_SECONDS}
      fps={FPS}
      width={1280}
      height={720}
    />
  );
};
