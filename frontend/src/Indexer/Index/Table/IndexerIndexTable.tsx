import { throttle } from 'lodash';
import React, { RefObject, useEffect, useRef, useState } from 'react';
import { useSelector } from 'react-redux';
import { FixedSizeList as List, ListChildComponentProps } from 'react-window';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import Scroller from 'Components/Scroller/Scroller';
import Column from 'Components/Table/Column';
import useMeasure from 'Helpers/Hooks/useMeasure';
import ScrollDirection from 'Helpers/Props/ScrollDirection';
import SortDirection from 'Helpers/Props/SortDirection';
import Indexer from 'Indexer/Indexer';
import dimensions from 'Styles/Variables/dimensions';
import getIndexOfFirstCharacter from 'Utilities/Array/getIndexOfFirstCharacter';
import IndexerIndexRow from './IndexerIndexRow';
import IndexerIndexTableHeader from './IndexerIndexTableHeader';
import styles from './IndexerIndexTable.css';

const bodyPadding = parseInt(dimensions.pageContentBodyPadding);
const bodyPaddingSmallScreen = parseInt(
  dimensions.pageContentBodyPaddingSmallScreen
);

interface RowItemData {
  items: Indexer[];
  sortKey: string;
  columns: Column[];
  isSelectMode: boolean;
  onCloneIndexerPress(id: number): void;
}

interface IndexerIndexTableProps {
  items: Indexer[];
  sortKey: string;
  sortDirection?: SortDirection;
  jumpToCharacter?: string;
  scrollTop?: number;
  scrollerRef: RefObject<HTMLElement>;
  isSelectMode: boolean;
  isSmallScreen: boolean;
  onCloneIndexerPress(id: number): void;
}

const columnsSelector = createSelector(
  (state: AppState) => state.indexerIndex.columns,
  (columns) => columns
);

function Row({ index, style, data }: ListChildComponentProps<RowItemData>) {
  const { items, sortKey, columns, isSelectMode, onCloneIndexerPress } = data;

  if (index >= items.length) {
    return null;
  }

  const indexer = items[index];

  return (
    <div
      style={{
        display: 'flex',
        justifyContent: 'space-between',
        ...style,
      }}
      className={styles.row}
    >
      <IndexerIndexRow
        indexerId={indexer.id}
        sortKey={sortKey}
        columns={columns}
        isSelectMode={isSelectMode}
        onCloneIndexerPress={onCloneIndexerPress}
      />
    </div>
  );
}

function getWindowScrollTopPosition() {
  return document.documentElement.scrollTop || document.body.scrollTop || 0;
}

function IndexerIndexTable(props: IndexerIndexTableProps) {
  const {
    items,
    sortKey,
    sortDirection,
    jumpToCharacter,
    isSelectMode,
    isSmallScreen,
    scrollerRef,
    onCloneIndexerPress,
  } = props;

  const columns = useSelector(columnsSelector);
  const listRef = useRef<List<RowItemData>>(null);
  const [measureRef, bounds] = useMeasure();
  const [size, setSize] = useState({ width: 0, height: 0 });
  const windowWidth = window.innerWidth;
  const windowHeight = window.innerHeight;

  const rowHeight = 38;

  useEffect(() => {
    const current = scrollerRef?.current as HTMLElement;

    if (isSmallScreen) {
      setSize({
        width: windowWidth,
        height: windowHeight,
      });

      return;
    }

    if (current) {
      const width = current.clientWidth;
      const padding =
        (isSmallScreen ? bodyPaddingSmallScreen : bodyPadding) - 5;

      setSize({
        width: width - padding * 2,
        height: windowHeight,
      });
    }
  }, [isSmallScreen, windowWidth, windowHeight, scrollerRef, bounds]);

  useEffect(() => {
    const currentScrollerRef = scrollerRef.current as HTMLElement;
    const currentScrollListener = isSmallScreen ? window : currentScrollerRef;

    const handleScroll = throttle(() => {
      const { offsetTop = 0 } = currentScrollerRef;
      const scrollTop =
        (isSmallScreen
          ? getWindowScrollTopPosition()
          : currentScrollerRef.scrollTop) - offsetTop;

      listRef.current?.scrollTo(scrollTop);
    }, 10);

    currentScrollListener.addEventListener('scroll', handleScroll);

    return () => {
      handleScroll.cancel();

      if (currentScrollListener) {
        currentScrollListener.removeEventListener('scroll', handleScroll);
      }
    };
  }, [isSmallScreen, listRef, scrollerRef]);

  useEffect(() => {
    if (jumpToCharacter) {
      const index = getIndexOfFirstCharacter(items, jumpToCharacter);

      if (index != null) {
        let scrollTop = index * rowHeight;

        // If the offset is zero go to the top, otherwise offset
        // by the approximate size of the header + padding (37 + 20).
        if (scrollTop > 0) {
          const offset = 57;

          scrollTop += offset;
        }

        listRef.current?.scrollTo(scrollTop);
        scrollerRef?.current?.scrollTo(0, scrollTop);
      }
    }
  }, [jumpToCharacter, rowHeight, items, scrollerRef, listRef]);

  return (
    <div ref={measureRef}>
      <Scroller
        className={styles.tableScroller}
        scrollDirection={ScrollDirection.Horizontal}
      >
        <IndexerIndexTableHeader
          columns={columns}
          sortKey={sortKey}
          sortDirection={sortDirection}
          isSelectMode={isSelectMode}
        />
        <List<RowItemData>
          ref={listRef}
          style={{
            width: '100%',
            height: '100%',
            overflow: 'none',
          }}
          width={size.width}
          height={size.height}
          itemCount={items.length}
          itemSize={rowHeight}
          itemData={{
            items,
            sortKey,
            columns,
            isSelectMode,
            onCloneIndexerPress,
          }}
        >
          {Row}
        </List>
      </Scroller>
    </div>
  );
}

export default IndexerIndexTable;
