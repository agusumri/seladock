import { Composition } from "remotion";
import { SelaDockScene } from "./SelaDockScene";
import { SelaDockReels } from "./SelaDockReels";

const FPS = 30;
const DURATION_SECONDS = 14;
const REELS_DURATION_SECONDS = 18;

export const RemotionRoot: React.FC = () => {
  return (
    <>
      <Composition
        id="SelaDock"
        component={SelaDockScene}
        durationInFrames={FPS * DURATION_SECONDS}
        fps={FPS}
        width={1280}
        height={720}
      />
      <Composition
        id="SelaDockReels"
        component={SelaDockReels}
        durationInFrames={FPS * REELS_DURATION_SECONDS}
        fps={FPS}
        width={1080}
        height={1920}
      />
    </>
  );
};
